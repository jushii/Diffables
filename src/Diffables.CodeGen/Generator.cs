using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Diffables.CodeGen
{
    [Generator]
    public class Generator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1. Get all classes decorated with Diffable attribute.
            IncrementalValuesProvider<INamedTypeSymbol> diffableClasses = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "Diffables.Core.DiffableAttribute",
                    (node, cancellationToken) => node is ClassDeclarationSyntax,
                    (context, cancellationToken) => (INamedTypeSymbol)context.TargetSymbol)
                .Where(symbol => symbol != null);

            // 2. Get all partial properties decorated with DiffableType attribute.
            IncrementalValuesProvider<IPropertySymbol> diffableTypeProperties = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "Diffables.Core.DiffableTypeAttribute",
                    (node, cancellationToken) =>
                    {
                        if (node is PropertyDeclarationSyntax propDecl)
                        {
                            return propDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
                        }
                        return false;
                    },
                    (context, cancellationToken) => (IPropertySymbol)context.TargetSymbol)
                .Where(property => property != null);

            // 3. Join diffable classes with their diffable properties.
            IncrementalValuesProvider<(INamedTypeSymbol DiffableClass, IEnumerable<(IPropertySymbol Property, bool IsDiffable)> Props)> diffablePropertiesByClass =
                diffableClasses.Combine(diffableTypeProperties.Collect())
                .Select((pair, cancellationToken) =>
                {
                    var diffableClass = pair.Left;
                    var allProperties = pair.Right;
                    var props = allProperties
                        .Where(prop => SymbolEqualityComparer.Default.Equals(prop.ContainingType, diffableClass))
                        .Select(prop => (Property: prop, IsDiffable: IsDiffableType(prop.Type)));
                    return (diffableClass, props);
                });

            // 4. Generate code for each diffable class.
            context.RegisterSourceOutput(diffablePropertiesByClass, (spc, tuple) =>
            {
                (INamedTypeSymbol symbol, IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties) = tuple;
                string namespaceName = symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : symbol.ContainingNamespace.ToDisplayString();
                string className = symbol.Name;
                StringBuilder sb = new StringBuilder();

                // Write usings.
                sb.AppendLine("using Diffables.Core;");
                sb.AppendLine("using System;");
                sb.AppendLine("using System.IO;");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(namespaceName))
                {
                    sb.AppendLine($"namespace {namespaceName}");
                    sb.AppendLine("{");
                }

                sb.AppendLine($"public partial class {className} : DiffableBase");
                sb.AppendLine("{");

                int propertyIndex = 0;
                foreach (var (property, isDiffable) in properties)
                {
                    sb.Append(GeneratePropertyCode(property, isDiffable, propertyIndex));
                    propertyIndex++;
                }

                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine(GenerateEncodeV2Method(properties));
                sb.AppendLine();
                sb.AppendLine(GenerateDecodeV2Method(properties));
                sb.AppendLine("}");

                if (!string.IsNullOrEmpty(namespaceName))
                {
                    sb.AppendLine("}");
                }

                spc.AddSource($"{className}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            });
        }

        /// <summary>
        /// Generates the code for a property including its backing field and accessor.
        /// </summary>
        private static string GeneratePropertyCode(IPropertySymbol property, bool isDiffable, int index)
        {
            string propertyType = property.Type.ToDisplayString();
            string propertyName = property.Name;
            string backingFieldName = $"_{propertyName.WithLowerFirstChar()}";
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"    private const uint BitmaskBit{propertyName} = 1 << {index};");
            sb.AppendLine($"    private {propertyType} {backingFieldName};");
            sb.AppendLine($"    public partial {propertyType} {propertyName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => {backingFieldName};");
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            if (isDiffable)
            {
                sb.AppendLine($"            if ({backingFieldName} != value)");
                sb.AppendLine("            {");
                sb.AppendLine($"                {backingFieldName} = value;");
                sb.AppendLine($"                if ({backingFieldName} != null)");
                sb.AppendLine("                {");
                sb.AppendLine("                     // Inform the nested Diffable property who its parent is.");
                sb.AppendLine($"                    {backingFieldName}.SetParent(this, BitmaskBit{propertyName});");
                sb.AppendLine("                }");
                sb.AppendLine($"                SetDirty(BitmaskBit{propertyName});");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine($"            if ({backingFieldName} != value)");
                sb.AppendLine("            {");
                sb.AppendLine($"                {backingFieldName} = value;");
                sb.AppendLine($"                SetDirty(BitmaskBit{propertyName});");
                sb.AppendLine("            }");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Generates the EncodeV2 method code.
        /// </summary>
        private static string GenerateEncodeV2Method(IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("    public override void EncodeV2(SerializationContext context)");
            sb.AppendLine("    {");
            sb.AppendLine($"        // Write header: RefId and bitmask for dirty properties.");
            sb.AppendLine("        context.Writer.Write(this.RefId);");
            sb.AppendLine("        context.Writer.Write(this._dirtyPropertiesBitmask);");
            sb.AppendLine();
            foreach (var (property, isDiffable) in properties)
            {
                string propertyName = property.Name;
                string propertyTypeName = property.Type.Name.ToString();
                sb.AppendLine($"        // Encode property: {propertyName}");
                sb.AppendLine($"        if ((this._dirtyPropertiesBitmask & BitmaskBit{propertyName}) != 0)");
                sb.AppendLine("        {");
                if (isDiffable)
                {
                    sb.AppendLine($"            if (this.{propertyName} == null)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                context.Writer.Write((byte)Operation.Delete);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                if (!context.Repository.TryGet(this.{propertyName}.RefId, out _))");
                    sb.AppendLine("                 {");
                    sb.AppendLine("                     // This instance has not been encoded before.");
                    sb.AppendLine("                     context.Writer.Write((byte)Operation.Add);");
                    sb.AppendLine($"                     context.Repository.Add(this.{propertyName});");
                    sb.AppendLine("                     // Recursively encode the nested property.");
                    sb.AppendLine($"                     this.{propertyName}.EncodeV2(context);");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 else");
                    sb.AppendLine("                 {");


                    sb.AppendLine("                     // The instance is already in the repository.");
                    sb.AppendLine("                     // If there are pending changes, propagate an update.");
                    sb.AppendLine($"                     if (this.{propertyName}.GetDirtyPropertiesBitmask() != 0)");
                    sb.AppendLine("                     {");
                    sb.AppendLine("                         context.Writer.Write((byte)Operation.Update);");
                    sb.AppendLine($"                         this.{propertyName}.EncodeV2(context);");
                    sb.AppendLine("                     }");
                    sb.AppendLine("                     else");
                    sb.AppendLine("                     {");
                    sb.AppendLine("                         // No changes, so just reference the already-sent instance.");
                    sb.AppendLine("                         context.Writer.Write((byte)Operation.AddByRefId);");
                    sb.AppendLine($"                         context.Writer.Write(this.{propertyName}.RefId);");
                    sb.AppendLine("                     }");


                    //sb.AppendLine("                     // Encode delta.");
                    //sb.AppendLine($"                     context.Writer.Write((byte)Operation.Update);");
                    //sb.AppendLine($"                     this.{propertyName}.EncodeV2(context);");
                    sb.AppendLine("                 }");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.AppendLine("            context.Writer.Write((byte)Operation.Update);");
                    sb.AppendLine($"            context.Writer.Write(this.{propertyName});");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("        // Clear our dirty state after encoding.");
            sb.AppendLine("        this.ResetDirtyPropertiesBitmask();");
            sb.AppendLine("    }");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the DecodeV2 method code.
        /// </summary>
        private static string GenerateDecodeV2Method(IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("    public override void DecodeV2(SerializationContext context)");
            sb.AppendLine("    {");
            sb.AppendLine("         // At this point we've already read the refId, so let's start by reading the dirty property bitmask.");
            sb.AppendLine("         uint dirtyPropertiesBitmask = context.Reader.ReadUInt32();");
            foreach (var (property, isDiffable) in properties)
            {
                string propertyName = property.Name;
                string propertyTypeName = property.Type.Name.ToString();
                if (isDiffable)
                {
                    sb.AppendLine($"         if ((dirtyPropertiesBitmask & BitmaskBit{propertyName}) != 0)");
                    sb.AppendLine("         {");
                    sb.AppendLine("             Operation op = (Operation)context.Reader.ReadByte();");
                    sb.AppendLine("             switch (op)");
                    sb.AppendLine("             {");
                    sb.AppendLine("                 case Operation.Delete:");
                    sb.AppendLine($"                     this.{propertyName} = null;");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 case Operation.Add:");
                    sb.AppendLine("                 {");
                    sb.AppendLine("                     // Create a new instance and decode it.");
                    sb.AppendLine($"                     int childRefId = context.Reader.ReadInt32();");
                    sb.AppendLine($"                     var instance = new {propertyTypeName}();");
                    sb.AppendLine($"                     instance.RefId = childRefId;");
                    sb.AppendLine("                     context.Repository.Add(instance);");
                    sb.AppendLine("                     instance.DecodeV2(context);");
                    sb.AppendLine($"                     this.{propertyName} = instance;");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 case Operation.Update:");
                    sb.AppendLine("                     {");
                    sb.AppendLine($"                        int refId = context.Reader.ReadInt32();");
                    sb.AppendLine($"                        this.{propertyName}.DecodeV2(context);");
                    sb.AppendLine("                     }");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 case Operation.AddByRefId:");
                    sb.AppendLine("                 {");
                    sb.AppendLine($"                     int refId = context.Reader.ReadInt32();");
                    sb.AppendLine($"                     if (context.Repository.TryGet(refId, out IDiffable existing))");
                    sb.AppendLine("                     {");
                    sb.AppendLine($"                         this.{propertyName} = ({propertyTypeName})existing;");
                    sb.AppendLine($"                         // Increase the reference count by 1.");
                    sb.AppendLine($"                         context.Repository.Add(existing);");
                    sb.AppendLine("                     }");
                    sb.AppendLine("                     else");
                    sb.AppendLine("                     {");
                    sb.AppendLine("                         throw new Exception($\"Trying to AddByRefId but a Diffable with RefId {refId} not found.\");");
                    sb.AppendLine("                     }");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 default:");
                    sb.AppendLine("                         throw new Exception($\"Unhandled/unexpected operation.\");");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("             }");
                    sb.AppendLine("         }");
                }
                else
                {
                    sb.AppendLine($"         if ((dirtyPropertiesBitmask & BitmaskBit{propertyName}) != 0)");
                    sb.AppendLine("         {");
                    sb.AppendLine("             Operation op = (Operation)context.Reader.ReadByte();");
                    sb.AppendLine("             if (op == Operation.Update)");
                    sb.AppendLine("             {");
                    switch (property.Type.Name.ToString())
                    {
                        case "Int32":
                            sb.AppendLine($"                 this.{propertyName} = context.Reader.ReadInt32();");
                            break;
                        case "String":
                            sb.AppendLine($"                 this.{propertyName} = context.Reader.ReadString();");
                            break;
                    }
                    sb.AppendLine("             }");
                    sb.AppendLine("             else");
                    sb.AppendLine("             {");
                    sb.AppendLine($"                 throw new Exception(\"Unexpected operation for {propertyName} property.\");");
                    sb.AppendLine("             }");
                    sb.AppendLine("         }");
                }
            }
            sb.AppendLine("    }");
            return sb.ToString();
        }

        ///// <summary>
        ///// Generates the Encode() method code.
        ///// </summary>
        //private static string GenerateEncodeMethod(IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    sb.AppendLine("    public byte[] Encode()");
        //    sb.AppendLine("    {");
        //    sb.AppendLine("        using var memoryStream = new MemoryStream();");
        //    sb.AppendLine("        using var binaryWriter = new BinaryWriter(memoryStream);");
        //    sb.AppendLine();
        //    sb.AppendLine("        binaryWriter.Write(this.RefId);");
        //    sb.AppendLine();

        //    foreach (var (property, isDiffable) in properties)
        //    {
        //        string propertyName = property.Name;
        //        string propertyTypeName = property.Type.Name.ToString();
        //        sb.AppendLine($"        // Encode property: {propertyName}");
        //        if (isDiffable)
        //        {
        //            sb.AppendLine($"        if (this.{propertyName} != null)");
        //            sb.AppendLine("        {");
        //            sb.AppendLine($"            byte[] encoded{propertyName} = this.{propertyName}.Encode();");
        //            sb.AppendLine($"            binaryWriter.Write(encoded{propertyName}.Length);");
        //            sb.AppendLine($"            binaryWriter.Write(encoded{propertyName});");
        //            sb.AppendLine("        }");
        //            sb.AppendLine("        else");
        //            sb.AppendLine("        {");
        //            sb.AppendLine("            binaryWriter.Write(0); // null");
        //            sb.AppendLine("        }");
        //        }
        //        else
        //        {
        //            byte propertyTypeCode = PropertyTypeCodes.GetPropertyTypeCodeFromPropertyTypeName(propertyTypeName);
        //            sb.AppendLine($"        binaryWriter.Write((byte){propertyTypeCode});");

        //            // Write primitive types. Extend this switch as needed.
        //            switch (property.Type.Name.ToString())
        //            {
        //                case "Int32":
        //                    sb.AppendLine($"        binaryWriter.Write(this.{propertyName});");
        //                    break;
        //                case "String":
        //                    sb.AppendLine($"        binaryWriter.Write(this.{propertyName});");
        //                    break;
        //                    // Add additional cases here...
        //            }
        //        }
        //        sb.AppendLine();
        //    }

        //    sb.AppendLine("        return memoryStream.ToArray();");
        //    sb.AppendLine("    }");
        //    return sb.ToString();
        //}

        ///// <summary>
        ///// Generates the Decode(byte[]) method code.
        ///// </summary>
        //private static string GenerateDecodeMethod(IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties)
        //{
        //    StringBuilder sb = new StringBuilder();
        //    sb.AppendLine("    public void Decode(byte[] bytes)");
        //    sb.AppendLine("    {");
        //    sb.AppendLine("        using var memoryStream = new MemoryStream(bytes);");
        //    sb.AppendLine("        using var binaryReader = new BinaryReader(memoryStream);");
        //    sb.AppendLine("        this.RefId = binaryReader.ReadInt32();");
        //    sb.AppendLine();

        //    foreach (var (property, isDiffable) in properties)
        //    {
        //        string propertyName = property.Name;
        //        string propertyTypeName = property.Type.Name.ToString();
        //        sb.AppendLine($"        // Decode property: {propertyName}");
        //        if (isDiffable)
        //        {
        //            sb.AppendLine($"        int {propertyName}Length = binaryReader.ReadInt32();");
        //            sb.AppendLine($"        if ({propertyName}Length > 0)");
        //            sb.AppendLine("        {");
        //            sb.AppendLine($"            byte[] {propertyName}Bytes = binaryReader.ReadBytes({propertyName}Length);");
        //            sb.AppendLine($"            this.{propertyName} = new {propertyTypeName}();");
        //            sb.AppendLine($"            this.{propertyName}.Decode({propertyName}Bytes);");
        //            sb.AppendLine("        }");
        //            sb.AppendLine("        else");
        //            sb.AppendLine("        {");
        //            sb.AppendLine($"            this.{propertyName} = null;");
        //            sb.AppendLine("        }");
        //        }
        //        else
        //        {
        //            sb.AppendLine($"        byte {propertyName}TypeCode = binaryReader.ReadByte();");
        //            switch (property.Type.Name.ToString())
        //            {
        //                case "Int32":
        //                    sb.AppendLine($"        this.{propertyName} = binaryReader.ReadInt32();");
        //                    break;
        //                case "String":
        //                    sb.AppendLine($"        this.{propertyName} = binaryReader.ReadString();");
        //                    break;
        //                    // Add additional cases here...
        //            }
        //        }
        //        sb.AppendLine();
        //    }

        //    sb.AppendLine("    }");
        //    return sb.ToString();
        //}

        /// <summary>
        /// Returns true if the type has a DiffableAttribute.
        /// </summary>
        private static bool IsDiffableType(ITypeSymbol type) =>
            type is INamedTypeSymbol namedType &&
            namedType.GetAttributes().Any(attr => attr.AttributeClass?.Name == "DiffableAttribute");

    }
}
