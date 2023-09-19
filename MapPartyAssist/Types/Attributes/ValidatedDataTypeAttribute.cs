using System;

namespace MapPartyAssist.Types.Attributes {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal class ValidatedDataTypeAttribute : Attribute {
    }
}
