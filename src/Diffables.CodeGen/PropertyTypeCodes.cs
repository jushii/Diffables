namespace Diffables.CodeGen
{
    internal static class PropertyTypeCodes
    {
        internal static byte GetPropertyTypeCodeFromPropertyTypeName(string propertyTypeName)
        {
            switch (propertyTypeName)
            {
                case "Int32":
                    return 1;
                case "String":
                    return 2;
            }
            return 0;
        }
    }
}
