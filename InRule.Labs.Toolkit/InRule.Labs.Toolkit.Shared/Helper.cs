﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InRule.Authoring.Commands;
using InRule.Authoring.Extensions;
using InRule.Repository;
using InRule.Repository.Attributes;
using InRule.Repository.RuleElements;
using System.IO;
using InRule.Labs.Toolkit.Shared.Model;
using InRule.Repository.Vocabulary;

namespace InRule.Labs.Toolkit.Shared
{
    /// <summary>
    /// Utility class suitable for all/or nothing import of toolkits and other ruleapps.
    /// </summary>
    public class Helper
    {
        //TODO: Refactor this member variable for thread safety
        private string _importHash = ""; //prevents duplicate import

        /// <summary>
        /// Returns a bindable collection for a XAML control.
        /// </summary>
        public ObservableCollection<ToolkitContents> GetToolkits(RuleApplicationDef dest)
        {
            ObservableCollection <ToolkitContents> toolkits = new ObservableCollection<ToolkitContents>();
            foreach (XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem att in dest.Attributes.Default)
            {
                if (att.Key.Contains("Toolkit:"))
                {
                    string key = att.Key.Substring(8, att.Key.Length - 8);  //trim toolkit prefix
                    ToolkitContents tk = new ToolkitContents();
                    ParseKey(key,tk);
                    tk.Contents = GetToolkitContents(key, dest);
                    toolkits.Add(tk);
                }
            }
            return toolkits;
        }
        
        internal bool ToolkitExists(RuleApplicationDef source, RuleApplicationDef dest)
        {
            bool exists = false;
            foreach (XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem att in dest.Attributes.Default)
            {
                if (att.Key.Contains("Toolkit:"))
                {
                    string key = att.Key.Substring(8, att.Key.Length - 8); //trim toolkit prefix
                    if (MakeKey(source) == key)
                    {
                        exists = true;
                        break;
                    }
                }
            }
            return exists;
        }
        
        internal void ParseKey(string key, ToolkitContents toolkit)
        {
            toolkit.Name = key.Split(',')[0];
            toolkit.Revision = key.Split(',')[1];
            toolkit.GUID = key.Split(',')[2];
        }
        internal ObservableCollection<RuleRepositoryDefBase> GetToolkitContents(string key, RuleApplicationDef dest)
        {
            ObservableCollection<RuleRepositoryDefBase> list = new ObservableCollection<RuleRepositoryDefBase>();
            //unpack the source ruleappdef
            RuleApplicationDef source = this.GetSourceRuleapp("Toolkit:" + key, dest);
            GetAll(source, list);
            return list;
        }
        internal void ValidateImport(RuleApplicationDef dest)
        {
            if (dest.Validate().Count != 0)
            {
                throw new InvalidImportException("The import you just attempted is not valid.");
            }
        }
        /// <summary>
        /// Gerneral import for ruleaps off the filesystem.
        /// </summary>
        public void ImportRuleApp(RuleApplicationDef source, RuleApplicationDef dest)
        {
            Import(source, dest, false);
            ValidateImport(dest);
        }
        public void ImportRuleApp(RuleApplicationDef source, RuleApplicationDef dest, string savePath)
        {
            ImportRuleApp(source, dest);
            dest.SaveToFile(savePath);
        }
        public void ImportRuleApp(string sourceRuleappPath, string destRuleappPath)
        {
            try
            {
                ImportRuleApp(RuleApplicationDef.Load(sourceRuleappPath),
                    RuleApplicationDef.Load(destRuleappPath), destRuleappPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace + ex.InnerException);
            }
        }
        /// <summary>
        /// Import a Rule Application as a toolkit off the filesystem.
        /// </summary>
        public void ImportToolkit(RuleApplicationDef source, RuleApplicationDef dest)
        {
            if (ToolkitExists(source, dest))
            {
                throw new DuplicateToolkitException("Toolkit already exists in the destination rule application.");
            }
            Import(source, dest, true);
            ValidateImport(dest);
            StoreSourceRuleapp(source,dest);
        }
        public void ImportToolkit(RuleApplicationDef source, RuleApplicationDef dest, string savePath)
        {
            ImportToolkit(source,dest);
            dest.SaveToFile(savePath);
        }
        public void ImportToolkit(string sourceRuleappPath, string destinationRuleappPath)
        {
            try
            {
                ImportToolkit(RuleApplicationDef.Load(sourceRuleappPath),
                    RuleApplicationDef.Load(destinationRuleappPath), destinationRuleappPath);    
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace + ex.InnerException);
            }
        }
        /// <summary>
        /// Remove a imported toolkit from a Rule Application.
        /// </summary>
        public void RemoveToolkit(RuleApplicationDef source, RuleApplicationDef dest)
        {
            string key = MakeKey(source);
            Remove(dest, key);
            RemoveSourceRuleapp(source, dest);
        }
        public void RemoveToolkit(RuleApplicationDef source, RuleApplicationDef dest, string savePath)
        {
            RemoveToolkit(source, dest);
            dest.SaveToFile(savePath);
        }
        public void RemoveToolkit(string sourceRuleappPath, string destinationRuleappPath)
        {
            try
            {
                RemoveToolkit(RuleApplicationDef.Load(sourceRuleappPath),
                    RuleApplicationDef.Load(destinationRuleappPath), destinationRuleappPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message + ex.StackTrace + ex.InnerException);
            }
        }
        /// <summary>
        /// Check if a specific ruleapp matches a key hash (Name,Revision,GUID).
        /// </summary>
        public bool IsToolkitMatch(RuleRepositoryDefBase def, string key)
        {
            var isMatch = false;
            var attributes =
                from XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem att in def.Attributes.Default
                where att.Value == key 
                select att;
            if (attributes.Any())
            {
                isMatch = true;
            }
            return isMatch;
        }
        /// <summary>
        /// Returns a key hash for a given Rule Application (Name,Revision,GUID).
        /// </summary>
        public string GetKey(RuleApplicationDef source)
        {
            return MakeKey(source);
        }
        internal string GetTmpPath()
        {
            return Path.GetTempPath() + Guid.NewGuid() + ".ruleappx";
        }
        internal string MakeKey(RuleApplicationDef source)
        {
            return source.Name + "," + source.Revision + "," + source.Guid;
        }
        internal void StoreSourceRuleapp(RuleApplicationDef source, RuleApplicationDef dest)
        {
            //Save temporarily to the filesystem
            string tmp = GetTmpPath();
            source.SaveToFile(tmp);
            string file = EncodeFile(tmp);
            //Store in target attribute with stamp
            string key = MakeKey(source);
            StoreFileInAttribute(file, key, dest);
        }
        internal void StoreFileInAttribute(string file, string key, RuleApplicationDef dest)
        {
            dest.Attributes.Default.Add("Toolkit:" + key, file);
        }
        internal string EncodeFile(string path)
        {
            //Base64Encode file
            byte[] bytes = File.ReadAllBytes(path);
            return Convert.ToBase64String(bytes);
        }
        internal void DecodeFile(string file, string path)
        {
            byte[] bytes = Convert.FromBase64String(file);
            File.WriteAllBytes(path, bytes);
        }
        /// <summary>
        /// Extracts a specific toolkit from a Rule Application.
        /// </summary>
        public RuleApplicationDef  GetSourceRuleapp(string key, RuleApplicationDef dest)
        {
            RuleApplicationDef def = null;
            //Get from attribute
            XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem att = FindSourceAttribute(key, dest);
            if (att != null)
            {
                string file = att.Value;     
                string tmpPath = GetTmpPath();
                DecodeFile(file, tmpPath);
                def = RuleApplicationDef.Load(tmpPath);
            }
            return def;
        }
        internal void RemoveSourceRuleapp(RuleApplicationDef source, RuleApplicationDef dest)
        {
            string stamp = "Toolkit:" + MakeKey(source);
            dest.Attributes.Default.Remove(stamp);
        }
        internal XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem FindSourceAttribute(string key, RuleApplicationDef dest)
        {
            XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem resultAtt = null;
            foreach (XmlSerializableStringDictionary.XmlSerializableStringDictionaryItem att in dest.Attributes.Default)
            {
                if (att.Key == key)
                {
                    resultAtt = att;
                    break;
                }
            }
            return resultAtt;
        }
        internal void Import(RuleApplicationDef source, RuleApplicationDef dest, bool toolkit)
        {
            string key = MakeKey(source);
            if (toolkit == true)
            {
                GetAll(source);  //stamps source artifacts with an attribute containing a toolkit key
            }
            //import entities
            foreach (RuleRepositoryDefBase entityDef in source.Entities)
            {  
                dest.Entities.Add(entityDef.CopyWithSameGuids());
            }
            //import rulesets
            foreach (RuleRepositoryDefBase rulesetDef in source.RuleSets)
            {
                dest.RuleSets.Add(rulesetDef.CopyWithSameGuids());
            }
            //import endpoints
            foreach (RuleRepositoryDefBase endpoint in source.EndPoints)
            {
                dest.EndPoints.Add(endpoint.CopyWithSameGuids());
            }
            //import udfs
            foreach (RuleRepositoryDefBase udf in source.UdfLibraries)
            {
                dest.UdfLibraries.Add(udf.CopyWithSameGuids());
            }
            //import categories
            foreach (RuleRepositoryDefBase category in source.Categories)
            {
                dest.Categories.Add(category.CopyWithSameGuids());
            }
            //data elements
            foreach (RuleRepositoryDefBase dataelement in source.DataElements)
            {
                dest.DataElements.Add(dataelement.CopyWithSameGuids());
            }
            //import vocabulary at the ruleapp level
            foreach (RuleRepositoryDefBase template in source.Vocabulary.Templates)
            {
                if (dest.Vocabulary == null)
                {
                    dest.Vocabulary = new VocabularyDef();
                }
                dest.Vocabulary.Templates.Add(template.CopyWithSameGuids());
            }
        }
        internal void Remove(RuleApplicationDef dest, string key)
        {
           
            //remove entities
            foreach (EntityDef entity in dest.Entities.ToList<RuleRepositoryDefBase>())
            {
                if (IsToolkitMatch(entity, key))
                {
                    dest.Entities.Remove(entity);
                }
            }
            //remove rulesets
            foreach (RuleRepositoryDefBase ruleset in dest.RuleSets.ToList<RuleRepositoryDefBase>())
            {
                if (IsToolkitMatch(ruleset, key))
                {
                    dest.RuleSets.Remove(ruleset);
                }
            }
            //remove endpoints
            foreach (RuleRepositoryDefBase endpoint in dest.EndPoints.ToList<RuleRepositoryDefBase>())
            {
                if (IsToolkitMatch(endpoint, key))
                {
                    dest.EndPoints.Remove(endpoint);
                }
            }
            //remove categories
            foreach (RuleRepositoryDefBase category in dest.Categories.ToList<RuleRepositoryDefBase>())
            {
                if (IsToolkitMatch(category, key))
                {
                    dest.Categories.Remove(category);
                }
            }
            //remove dataelements
            foreach (RuleRepositoryDefBase dataelement in dest.DataElements.ToList<RuleRepositoryDefBase>())
            {
                if (IsToolkitMatch(dataelement, key))
                {
                    dest.DataElements.Remove(dataelement);
                }
            }
            //remove UDFs
            foreach (RuleRepositoryDefBase udf in dest.UdfLibraries.ToList<RuleRepositoryDefBase>())
            {
                if (IsToolkitMatch(udf, key))
                {
                    dest.UdfLibraries.Remove(udf);
                }
            }
        }
        /// <summary>
        /// It's ok to add attributes to TemplateDefs but not their children.
        /// </summary>
        internal bool IsSafeTemplateDef(RuleRepositoryDefBase child)
        {
            bool isSafe = true;
            if (child.GetType().ToString().Contains("InRule.Repository.Vocabulary"))
            {
                string prefix = "InRule.Repository.Vocabulary.";
                string longname = child.GetType().ToString();
                string shortname = longname.Substring(prefix.Length,longname.Length - prefix.Length);
                if (child.GetType() != typeof(TemplateDef))
                {
                    isSafe = false;
                }
            }
            return isSafe;
        }
        internal void ProcessChildren(RuleRepositoryDefBase child, ObservableCollection<RuleRepositoryDefBase> list, string key)
        {
            if (_importHash.Contains(child.Name) == false)
            {
                _importHash = _importHash + child.Name;  //update the hash
                Console.WriteLine(child.Name);
                if (String.IsNullOrEmpty(key) == false)
                {
                   if (IsSafeTemplateDef(child)) //some vocab definitions are not safe to stamp with an attribute
                   {
                       StampAttribute(child, key);
                   }
                }
                list?.Add(child);
                var collquery = from childcollections in child.GetAllChildCollections()
                                select childcollections;
                foreach (RuleRepositoryDefCollection defcollection in collquery)
                {
                    var defquery = from RuleRepositoryDefBase items in defcollection select items;
                    foreach (var def in defquery)
                    {
                        ProcessChildren(def, list, key);
                    }
                }
            }
        }
        internal void StampAttribute(RuleRepositoryDefBase def, string key)
        {
            Debug.WriteLine(def.Name);
            //if for whatever reason it's already been stamped
            if (IsToolkitMatch(def, key) == false)
            {
                def.Attributes.Default.Add("Toolkit", key);
            }
        }

        internal void GetAll(RuleApplicationDef source)
        {
            GetAll(source, null);
        }
        internal void GetAll(RuleApplicationDef source, ObservableCollection<RuleRepositoryDefBase> list)
        {
            _importHash = "";  //reset
            string key = MakeKey(source);
            RuleRepositoryDefCollection[] colls = source.GetAllChildCollections();
            foreach (RuleRepositoryDefCollection coll in colls)
            {
                foreach (RuleRepositoryDefBase def in coll)
                {
                        ProcessChildren(def, list, key);
                }
            }
        }
    }
}
