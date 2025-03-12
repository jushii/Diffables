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
                sb.AppendLine(GenerateEncodeMethod(properties));
                sb.AppendLine(GenerateDecodeMethod(properties));
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
            string bitmaskBitProperty = $"BitmaskBit{propertyName}";
            sb.AppendLine($"    private const uint BitmaskBit{propertyName} = 1 << {index};");
            sb.AppendLine($"    private {propertyType} {backingFieldName};");
            sb.AppendLine($"    public partial {propertyType} {propertyName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => {backingFieldName};");
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            if (isDiffable)
            {
                sb.AppendLine($"            if ({backingFieldName} == value) return;");
                sb.AppendLine($"            Operation op = Operation.None;");
                sb.AppendLine($"            if ({backingFieldName} == null)");
                sb.AppendLine("            {");
                sb.AppendLine($"                 op = Operation.AddByRefId;");
                sb.AppendLine($"                 // RefCount 0 means this is the first time we add a reference to this instance.");
                sb.AppendLine($"                 if (value.RefCount == 0) op = Operation.Add;");
                sb.AppendLine($"                 {backingFieldName} = value;");
                sb.AppendLine($"                 {backingFieldName}.OnSetDirty += On{propertyName}Dirty;");
                sb.AppendLine($"                 value.RefCount++;");
                sb.AppendLine($"                 SetDirty({bitmaskBitProperty}, op);");
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                sb.AppendLine("                 if (value == null)");
                sb.AppendLine("                 {");
                sb.AppendLine($"                     op = Operation.Delete;"); // TODO: Do we need DeleteByRefId?
                sb.AppendLine($"                     {backingFieldName}.RefCount--;");
                sb.AppendLine($"                     {backingFieldName}.OnSetDirty -= On{propertyName}Dirty;");
                sb.AppendLine($"                     {backingFieldName} = null;");
                sb.AppendLine($"                     SetDirty({bitmaskBitProperty}, op);");
                sb.AppendLine("                 }");
                sb.AppendLine("                 else");
                sb.AppendLine("                 {");
                sb.AppendLine("                     // TODO: Handle replace");
                sb.AppendLine("                 }");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine($"            if ({backingFieldName} != value)");
                sb.AppendLine("            {");
                sb.AppendLine($"                {backingFieldName} = value;");
                sb.AppendLine($"                SetDirty({bitmaskBitProperty}, Operation.Update);");
                sb.AppendLine("            }");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine($"    private void On{propertyName}Dirty() {{ SetDirty(BitmaskBit{propertyName}, Operation.Update); }}");
            sb.AppendLine("");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the Encode method code.
        /// </summary>
        private static string GenerateEncodeMethod(IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("    public override void Encode(SerializationContext context)");
            sb.AppendLine("    {");
            sb.AppendLine($"        // Write header: RefId and bitmask for dirty properties.");
            sb.AppendLine("        context.Writer.Write(this.RefId);");
            sb.AppendLine("        context.Writer.Write(this.ChangeTree.DirtyPropertiesBitmask);");
            sb.AppendLine();
            foreach (var (property, isDiffable) in properties)
            {
                string propertyName = property.Name;
                string propertyTypeName = property.Type.Name.ToString();
                string bitmaskBitProperty = $"BitmaskBit{propertyName}";
                sb.AppendLine($"        // Encode property: {propertyName}");
                sb.AppendLine($"        if ((this.ChangeTree.DirtyPropertiesBitmask & {bitmaskBitProperty}) != 0)");
                sb.AppendLine("        {");
                if (isDiffable)
                {
                    sb.AppendLine($"            Operation op = this.ChangeTree.Operations[{bitmaskBitProperty}];");
                    sb.AppendLine($"            context.Writer.Write((byte)op);");
                    sb.AppendLine($"            switch (op)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                 case Operation.Update:");
                    sb.AppendLine("                 {");
                    sb.AppendLine($"                     this.{propertyName}.Encode(context);");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 case Operation.Add:");
                    sb.AppendLine("                 {");
                    sb.AppendLine($"                     this.{propertyName}.Encode(context);");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 case Operation.AddByRefId:");
                    sb.AppendLine("                 {");
                    sb.AppendLine($"                     context.Writer.Write({propertyName}.RefId);");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 case Operation.Delete:");
                    sb.AppendLine("                 {");
                    sb.AppendLine($"                     // We've already set {propertyName} to null so we don't have it's RefId,");
                    sb.AppendLine($"                     // but at decoding phase we know what field is being decoded so we can act accordingly..");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("            }");
                }
                else
                {
                    sb.AppendLine($"            context.Writer.Write((byte)this.ChangeTree.Operations[{bitmaskBitProperty}]);");
                    sb.AppendLine($"            context.Writer.Write(this.{propertyName});");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            sb.AppendLine("        this.ChangeTree.Clear();");
            sb.AppendLine("    }");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the Decode method code.
        /// </summary>
        private static string GenerateDecodeMethod(IEnumerable<(IPropertySymbol Property, bool IsDiffable)> properties)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("    public override void Decode(SerializationContext context)");
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
                    sb.AppendLine("                     instance.Decode(context);");
                    sb.AppendLine($"                     this.{propertyName} = instance;");
                    sb.AppendLine("                     break;");
                    sb.AppendLine("                 }");
                    sb.AppendLine("                 case Operation.Update:");
                    sb.AppendLine("                     {");
                    sb.AppendLine($"                        int refId = context.Reader.ReadInt32();");
                    sb.AppendLine($"                        this.{propertyName}.Decode(context);");
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

        /// <summary>
        /// Returns true if the type has a DiffableAttribute.
        /// </summary>
        private static bool IsDiffableType(ITypeSymbol type) =>
            type is INamedTypeSymbol namedType &&
            namedType.GetAttributes().Any(attr => attr.AttributeClass?.Name == "DiffableAttribute");

    }
}
