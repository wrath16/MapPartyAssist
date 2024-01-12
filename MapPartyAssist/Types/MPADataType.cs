namespace MapPartyAssist.Types {

    //represents a data type that will be stored in the database
    public abstract class MPADataType {

        public int MigrationLevel { get; set; }

        //returns true if data is all valid
        //public bool Validate(bool correctErrors = false) {
        //    bool isValid = true;
        //    var nullabilityContext = new NullabilityInfoContext();
        //    PluginLog.Debug($"Type: {GetType().Name}");
        //    foreach(var prop in GetType().GetProperties()) {
        //        var nullabilityInfo = nullabilityContext.Create(prop);
        //        bool nullable = nullabilityInfo.WriteState is NullabilityState.Nullable;
        //        var curValue = prop.GetValue(this);
        //        bool isNull = curValue is null;
        //        bool isEnumerable = typeof(IEnumerable<MPADataType>).IsAssignableFrom(prop.PropertyType);
        //        bool isReference = prop.GetCustomAttribute(typeof(BsonRefAttribute), true) != null;
        //        bool isDataType = typeof(MPADataType).IsAssignableFrom(prop.PropertyType);
        //        //bool isDataType = curValue is MPADataType;
        //        PluginLog.Debug(string.Format("Name:  {0, -20} Type: {1, -15} IsEnumerable: {4, -6} IsDataType: {5, -6} IsReference: {6, -6} Nullable: {2, -6} IsNull: {3,-6}", prop.Name, prop.PropertyType.Name, nullable, isNull, isEnumerable, isDataType, isReference));

        //        //check recursive data type
        //        if(isDataType && !isReference && !isNull && !((MPADataType)curValue!).Validate()) {
        //            isValid = false;
        //        }

        //        //check enumerable
        //        if(isEnumerable && !isNull && prop.PropertyType != typeof(string)) {
        //            var enumerable = (IEnumerable<MPADataType>)curValue!;

        //            foreach(var element in (IEnumerable<MPADataType>)curValue!) {
        //                isValid = element.Validate() && isValid;
        //            }
        //        }

        //        if(!nullable && isNull) {
        //            isValid = false;
        //            //if(correctErrors) {
        //            //    //invoke default constructor if it has fixes
        //            //    var defaultCtor = prop.PropertyType.GetConstructor(Type.EmptyTypes);
        //            //    if(defaultCtor != null) {
        //            //        curValue = defaultCtor.Invoke(null);
        //            //    } else {
        //            //        //PluginLog.Warning($"No default constructor for type: {prop.PropertyType.Name}");
        //            //    }
        //            //}
        //        }
        //    }
        //    return isValid;
        //}
    }
}
