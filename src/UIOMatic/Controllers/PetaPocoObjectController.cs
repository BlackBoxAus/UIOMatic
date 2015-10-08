﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using UIOMatic.Atributes;
using UIOMatic.Interfaces;
using UIOMatic.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using Umbraco.Web.Editors;
using Umbraco.Web.Mvc;
using umbraco.IO;
using Umbraco.Core;

namespace UIOMatic.Controllers
{
    [PluginController("UIOMatic")]
    public class PetaPocoObjectController : UmbracoAuthorizedJsonController, IUIOMaticObjectController
    {

        public IEnumerable<object> GetAll(string typeName)
        {
            var currentType = Type.GetType(typeName);
            var tableName = (TableNameAttribute)Attribute.GetCustomAttribute(currentType, typeof(TableNameAttribute));


            var query = new Sql().Select("*").From(tableName.Value);

            foreach (dynamic item in DatabaseContext.Database.Fetch<dynamic>(query))
            {
                // get settable public properties of the type
                var props = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.GetSetMethod() != null);

                // create an instance of the type
                var obj = Activator.CreateInstance(currentType);
                
                // set property values using reflection
                var values = (IDictionary<string, object>)item;
                foreach (var prop in props)
                    prop.SetValue(obj, values[prop.Name]);

                yield return obj;
            }
            

            
        }

        public IEnumerable<UIOMaticPropertyInfo> GetAllProperties(string typeName)
        {
            var ar = typeName.Split(',');
            var currentType = Type.GetType(ar[0] + ", "+ ar[1]);
            foreach (var prop in currentType.GetProperties())
            {
                if (prop.Name != "UmbracoTreeNodeName")
                {
                    var attris = prop.GetCustomAttributes();

                    if (attris.All(x => x.GetType() != typeof (UIOMaticIgnoreFieldAttribute)))
                    {

                        if (attris.Any(x => x.GetType() == typeof (UIOMaticFieldAttribute)))
                        {
                            var attri =
                                (UIOMaticFieldAttribute)
                                    attris.SingleOrDefault(x => x.GetType() == typeof (UIOMaticFieldAttribute));

                            var pi = new UIOMaticPropertyInfo
                            {
                                Key = prop.Name,
                                Name = attri.Name,
                                Description = attri.Description,
                                //Required = attri.Required,
                                View = IOHelper.ResolveUrl(attri.GetView())
                            };
                            yield return pi;
                        }
                        else
                        {
                            var pi = new UIOMaticPropertyInfo
                            {
                                Key = prop.Name,
                                Name = prop.Name,
                                Description = string.Empty,
                                //Required = false,
                                View = IOHelper.ResolveUrl("~/App_Plugins/UIOMatic/Backoffice/Views/textfield.html")
                            };
                            yield return pi;
                        }
                    }

                }
            }

        }
        public object GetById(string typeName, int id)
        {


            var ar = typeName.Split(',');
            var currentType = Type.GetType(ar[0] + ", " + ar[1]);
            var tableName = ((TableNameAttribute)Attribute.GetCustomAttribute(currentType, typeof(TableNameAttribute))).Value;
            var primaryKeyColum = "id";
            foreach (var property in currentType.GetProperties())
            {
                var keyAttri = property.GetCustomAttributes().Where(x => x.GetType() == typeof (PrimaryKeyColumnAttribute));
                if (keyAttri.Any())
                    primaryKeyColum = property.Name;
            }
            return DatabaseContext.Database.Query<dynamic>(Sql.Builder
                .Append("SELECT * FROM " + tableName)
                .Append("WHERE "+primaryKeyColum+"=@0", id));

           
        }

        public object PostCreate(ExpandoObject objectToCreate)
        {
            var typeOfObject = objectToCreate.FirstOrDefault(x => x.Key == "typeOfObject").Value.ToString();
            objectToCreate = (ExpandoObject)objectToCreate.FirstOrDefault(x => x.Key == "objectToCreate").Value;

            var ar = typeOfObject.Split(',');
            var currentType = Type.GetType(ar[0] + ", " + ar[1]);

            object ob = Activator.CreateInstance(currentType, null);

            foreach (var prop in objectToCreate)
            {
                if (prop.Value != null)
                {
                    PropertyInfo propI = currentType.GetProperty(prop.Key);
                    Helper.SetValue(ob, propI.Name, prop.Value);

                }
            }

            DatabaseContext.Database.Save(ob);

            return ob;

        }

        public object PostUpdate(ExpandoObject objectToUpdate)
        {
            var typeOfObject = objectToUpdate.FirstOrDefault(x => x.Key == "typeOfObject").Value.ToString();
            objectToUpdate = (ExpandoObject)objectToUpdate.FirstOrDefault(x => x.Key == "objectToUpdate").Value;

            var ar = typeOfObject.Split(',');
            var currentType = Type.GetType(ar[0] + ", " + ar[1]);
        
            object ob = Activator.CreateInstance(currentType,null);

            foreach (var prop in objectToUpdate)
            {
                PropertyInfo propI = currentType.GetProperty(prop.Key);
                if (propI != null)
                {
                    
                    Helper.SetValue(ob, propI.Name, prop.Value);
                }
            }

            DatabaseContext.Database.Update(ob);

            return ob;
        }

        public int DeleteById(string typeOfObject, int id)
        {
            var currentType = Helper.GetTypesWithUIOMaticAttribute().First(x => x.AssemblyQualifiedName == typeOfObject);
            var tableName = ((TableNameAttribute)Attribute.GetCustomAttribute(currentType, typeof(TableNameAttribute))).Value;
            
            var primaryKeyTable = string.Empty;

            foreach (var prop in currentType.GetProperties())
            {
                foreach (var attri in prop.GetCustomAttributes(true))
                {
                    if (attri.GetType() == typeof (PrimaryKeyColumnAttribute))
                        primaryKeyTable = ((PrimaryKeyColumnAttribute)attri).Name ?? prop.Name;

                }
                
                
            }
            return DatabaseContext.Database.Delete(tableName, primaryKeyTable, null, id);

        }

        [HttpPost]
        public IEnumerable<Exception> Validate(ExpandoObject objectToValidate)
        {
            var typeOfObject = objectToValidate.FirstOrDefault(x => x.Key == "typeOfObject").Value.ToString();
            objectToValidate = (ExpandoObject)objectToValidate.FirstOrDefault(x => x.Key == "objectToValidate").Value;

            var ar = typeOfObject.Split(',');
            var currentType = Type.GetType(ar[0] + ", " + ar[1]);
          

            object ob = Activator.CreateInstance(currentType, null);

            var values = (IDictionary<string, object>)objectToValidate;
            foreach (var prop in currentType.GetProperties())
            {
                if (values.ContainsKey(prop.Name))
                {
                    Helper.SetValue(ob, prop.Name, values[prop.Name]);
                }
            }
                

            return ((IUIOMaticModel) ob).Validate();
        }
    }
}