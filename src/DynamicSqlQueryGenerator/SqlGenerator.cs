using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DynamicSqlQueryGenerator
{
    public class SqlGenerator<T>
    {
        /// <summary>
        /// Get Insert SQL Query based on available values in entity's properties
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="tableName">tableName will be used to insert data if tableName not supplied then entity class name will be used as TableName  </param>
        /// <param name="getScopeIdentity"> if true then query will have SELECT SCOPE_IDENTITY() statement in then end</param>
        /// <param name="excludedColumnName"> Here basically you can pass all column which exist in entity but not actually exists in Table </param>
        /// <returns> returns insert query for given tableName/entity name</returns>
        public static string GetInsertQuery(object entity, string tableName = "", bool getScopeIdentity = true,
            string[] excludedColumnName = null)
        {
            var mainQuery = new StringBuilder();
            var paramQuery = new StringBuilder();
            Type type = typeof(T);
            PropertyInfo[] propertyInfo = entity.GetType().GetProperties();

            foreach (PropertyInfo info in propertyInfo)
            {
                var value = info.GetValue(entity, null);
                DbType datatype = GetDataType(info.PropertyType);
                if (SkipUnwantedRecord(excludedColumnName, info, datatype, value)) continue;

                //Read the custom attribute "AlwaysSaveOrUdpate" which will make sure to save in db evertime
                bool isAlwaysSave = info.GetCustomAttributes(false).Any(a => a.GetType() == typeof(AlwaysSave));

                if (isAlwaysSave || value != null && datatype != DbType.Object && value.ToString() != "0")
                {
                    if (mainQuery.ToString() == string.Empty)
                    {
                        mainQuery.AppendFormat(
                            @"
                                INSERT INTO {0} 
                                (
                                  {1} ", GetTableName(tableName, entity), info.Name);

                        paramQuery.AppendFormat(" @{0}", info.Name);
                    }
                    else
                    {
                        mainQuery.AppendFormat(", {0}", info.Name);
                        paramQuery.AppendFormat(", @{0}", info.Name);
                    }
                }
            }
            if (mainQuery.ToString() != string.Empty)
            {
                mainQuery.AppendFormat(@"
                            ) 
                            VALUES
                            (
                                    {0}
                            )", paramQuery);

                if (getScopeIdentity)
                {
                    //This will get inserted id
                    mainQuery.Append(
                        @"
                                SELECT CAST(SCOPE_IDENTITY() as int) 
                            ");
                }
            }

            return mainQuery.ToString();
        }

        /// <summary>
        /// Get Update SQL Query based on available values in entity's properties
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="tableName">tableName will be used to insert data if tableName not supplied then entity class name will be used as TableName  </param>
        /// <param name="idColumnName">here we pass Id Column name will be used in where condition - default is "Id"</param>
        /// <param name="excludedColumnName"> Here basically you can pass all column which exist in entity but not actually exists in Table </param>
        /// <returns> returns Update SQL query for given tableName/entity name</returns>
        public static string GetUpdateQuery(object entity, string tableName = "", 
            string idColumnName = "Id", 
            string[] excludedColumnName = null)
        {
            var sqlQuery = new StringBuilder();
            Type type = typeof(T);
            PropertyInfo[] propertyInfo = entity.GetType().GetProperties();

            foreach (PropertyInfo info in propertyInfo)
            {
                DbType datatype = GetDataType(info.PropertyType);
                object value = info.GetValue(entity, null);
                if (SkipUnwantedRecord(excludedColumnName, info, datatype, value)) continue;

                //Read the custom attribute "AlwaysSaveOrUdpate" which will make sure to save in db evertime
                bool isAlwaysUdpate = info.GetCustomAttributes(false).Any(a => a.GetType() == typeof(AlwaysUdpate));

                if (isAlwaysUdpate ||
                    (datatype != DbType.Object && value != null && value.ToString() != "" && value.ToString() != "0"))
                {
                    if (sqlQuery.ToString() == string.Empty)
                    {
                        sqlQuery.AppendFormat(
                            @"
                                Update {0} Set {1}=@{1} ", GetTableName(tableName, entity), info.Name);
                    }
                    else
                    {
                        sqlQuery.AppendFormat(", {0} = @{0} ", info.Name);
                    }
                }
            }

            if (sqlQuery.ToString() != string.Empty)
            {
                sqlQuery.AppendFormat(
                    @" 
                        Where {0} = @{0}  ", idColumnName);
            }

            return sqlQuery.ToString();
        }

        private static string GetTableName(string tableName, object entity)
        {
            return string.IsNullOrEmpty(tableName) ? entity.GetType().Name : tableName;
        }

        private static bool SkipUnwantedRecord(string[] excludedColumnName, PropertyInfo info, DbType datatype, object value)
        {
            if (excludedColumnName != null && excludedColumnName.Contains(info.Name))
                return true;

            if (FieldsToIgnore.Contains(info.Name))
                return true;

            //DateTime datatype when not assigned then will have default value -- "1/1/0001 12:00:00 AM"
            if (datatype == DbType.DateTime && value != null && (DateTime)value == default(DateTime))
                return true;

            return false;
        }

        private static DbType GetDataType(Type propertyType)
        {
            //In Case property is Nullable type then we need to get actual data type lying under the Nullable type
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                propertyType = propertyType.GetGenericArguments()[0];
            }

            DbType datatype = GetDatatype(propertyType.FullName);

            //In case of type is enum
            if (propertyType.IsEnum)
            {
                datatype = DbType.Int32;
            }
            return datatype;
        }

        /// <summary>
        /// Here you can add all common fields usually you keep in datamodel/entity classes but doesnt exist in table
        /// </summary>
        private static readonly string[] FieldsToIgnore =
        {
            //"CreatedByFirstName", "CreatedByMiddleName", "CreatedByLastName", "LastEditedByFirstName",
            //"LastEditedByMiddleName", "LastEditedByLastName"
        };


        private static DbType GetDatatype(string dataType)
        {
            DbType dbType = DbType.Object;

            switch (dataType.ToString())
            {
                case "System.String":
                    dbType = DbType.String;
                    break;
                case "System.Int32":
                    dbType = DbType.Int32;
                    break;
                case "System.Int64":
                    dbType = DbType.Int64;
                    break;
                case "System.Guid":
                    dbType = DbType.Guid;
                    break;
                case "System.Boolean":
                    dbType = DbType.Boolean;
                    break;
                case "System.Date":
                    dbType = DbType.Date;
                    break;
                case "System.Decimal":
                    dbType = DbType.Decimal;
                    break;
                case "System.DateTime":
                    dbType = DbType.DateTime;
                    break;
                case "System.Double":
                    dbType = DbType.Double;
                    break;
                case "System.Single":
                    dbType = DbType.Double;
                    break;
            }

            return dbType;
        }

    }


}