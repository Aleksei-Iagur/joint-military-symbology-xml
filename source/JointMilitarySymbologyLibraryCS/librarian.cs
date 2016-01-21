﻿/* Copyright 2014 - 2015 Esri
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace JointMilitarySymbologyLibrary
{
    internal enum StatusCodeEnum
    {
        statusCodeNoVersion,
        statusCodeNoContext,
        statusCodeNoDimension,
        statusCodeNoStandardIdentity,
        statusCodeNoSymbolSet,
        statusCodeNoStatus,
        statusCodeNoHQTFDummy,
        statusCodeNoAmplifierGroup,
        statusCodeNoAmplifier,
        statusCodeNoAffiliation,
        statusCodeNoContextAmplifier,
        statusCodeNoEntity,
        statusCodeNoEntityType,
        statusCodeNoEntitySubType,
        statusCodeNoModifierOne,
        statusCodeNoModifierTwo,
        statusCodeNoLegacySymbol
    }

    public class Librarian
    {
        // This class is the work horse of the library.  It wraps the
        // auto-generated schema-focused classes under base.cs.

        // It sets up a logger (NLog) for use throughout the code base.

        // It initiates the deserealization of the JMSML XML data that 
        // is specified in the active config file (jmsml.config).

        // It holds a collection of all the SymbolSet objects that are
        // also deserialized.

        // It provides methods to assist in searching the deserialized
        // objects for a given JMSML element, using either 2525D number 
        // codes or 2525C letter codes.

        // It also serves as the sole way to create a Symbol object.

        private const string _domainSeperator = " : ";

        private string _configPath;
        private JMSMLConfig _configData;
        private Library _library;

        private bool _logConversion = true;
        private int _statusFlag = 0;

        private Symbol _invalidSymbol;
        private Symbol _retiredSymbol;

        private bool _drawColoredOCABars = true;
        private bool _drawCivilianFrames = false;

        private SortedDictionary<ushort, SymbolSet> _sortedSymbolSets = new SortedDictionary<ushort,SymbolSet>();
        private List<SymbolSet> _symbolSets = new List<SymbolSet>();
        private List<string> _statusMessages = new List<string> {"Version Not Found",
                                                                 "Context Not Found",
                                                                 "Dimension Not Found",
                                                                 "Standard Identity Not Found",
                                                                 "Symbol Set Not Found",
                                                                 "Status Not Found",
                                                                 "HQ/TF/Dummy Not Found",
                                                                 "Amplifier Group Not Found",
                                                                 "Amplifier Not Found",
                                                                 "Affiliation Not Found",
                                                                 "Context Amplifier Not Found",
                                                                 "Entity Not Found",
                                                                 "Entity Type Not Found",
                                                                 "Entity SubType Not Found",
                                                                 "Modifier One Not Found",
                                                                 "Modifier Two Not Found",
                                                                 "Legacy Symbol Not Found"};

        protected static Logger logger = LogManager.GetCurrentClassLogger();

        public Librarian(string configPath = "")
        {
            InitializeNLog();

            //
            // Deserialize the configuration xml to get the location of the symbology library
            //

            XmlSerializer serializer = new XmlSerializer(typeof(JMSMLConfig));

            serializer.UnknownNode += new XmlNodeEventHandler(serializer_UnknownNode);
            serializer.UnknownAttribute += new XmlAttributeEventHandler(serializer_UnknownAttribute);

            if (configPath != "")
            {
                _configPath = configPath;
            }
            else
            {
                string s = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                _configPath = Path.Combine(s, "jmsml.config");
            }

            if (File.Exists(_configPath))
            {
                using(FileStream fs = new FileStream(_configPath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.CanRead)
                    {
                        _configData = (JMSMLConfig)serializer.Deserialize(fs);
                    }
                    else
                    {
                        logger.Error("Unreadable config file: " + _configPath);
                    }
                }
            }
            else
            {
                logger.Error("Config file is missing: " + _configPath);
            }

            //
            // If the config data was good then...
            //

            if(_configData != null)
            {
                //
                // Deserialize the library's base xml to get the base contents of the symbology standard
                //

                serializer = new XmlSerializer(typeof(Library));

                string path = _configData.LibraryPath + "/" + _configData.LibraryName;

                if (File.Exists(path))
                {
                    using (FileStream fsLibrary = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        if (fsLibrary.CanRead)
                        {
                            this._library = (Library)serializer.Deserialize(fsLibrary);

                            //
                            // Deserialize each symbolSet xml
                            //

                            foreach (LibraryDimension dimension in this._library.Dimensions)
                            {
                                foreach (LibraryDimensionSymbolSetRef ssRef in dimension.SymbolSets)
                                {
                                    ushort ssCode = CodeToShort(ssRef.SymbolSetCode);
                                    if (!_sortedSymbolSets.ContainsKey(ssCode))
                                    {
                                        path = _configData.LibraryPath + "/" + ssRef.Instance;
                                        if (File.Exists(path))
                                        {
                                            using (FileStream fsSymbolSet = new FileStream(path, FileMode.Open, FileAccess.Read))
                                            {
                                                if (fsSymbolSet.CanRead)
                                                {
                                                    serializer = new XmlSerializer(typeof(SymbolSet));
                                                    SymbolSet ss = (SymbolSet)serializer.Deserialize(fsSymbolSet);

                                                    if (ss != null)
                                                    {
                                                        _sortedSymbolSets.Add(ssCode, ss);
                                                        _symbolSets.Add(ss);
                                                    }
                                                }
                                                else
                                                {
                                                    logger.Error("Unreadable symbol set: " + path);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            logger.Error("Symbol set is missing: " + path);
                                        }
                                    }
                                }
                            }

                            //
                            // Create special invalid and retired symbols for this library
                            //

                            _invalidSymbol = new Symbol(this, SIDC.INVALID);
                            _retiredSymbol = new Symbol(this, SIDC.RETIRED);
                        }
                        else
                        {
                            logger.Error("Unreadable symbol library: " + path);
                        }
                    }
                }
                else
                {
                    logger.Error("Specified library is missing: " + path);
                }
            }
        }

        private ushort CodeToShort(DoubleDigitType code)
        {
            if (code.DigitOne == 0)
                return code.DigitTwo;
            else
                return (ushort)(code.DigitOne * 10 + code.DigitTwo);
        }

        private void InitializeNLog()
        {
            Target logTarget = new FileTarget();

            ((FileTarget)logTarget).FileName = @"${specialfolder:MyDocuments}/jmsml/logs/${shortdate}.log";

            ((FileTarget)logTarget).Layout = new SimpleLayout("${longdate} | ${level} | ${callsite} | ${message} | ${exception:format=ToString}");

            /*
             * Write the log asynchronously
             */
            AsyncTargetWrapper wrapper = new AsyncTargetWrapper();
            wrapper.WrappedTarget = logTarget;
            wrapper.QueueLimit = 5000;
            wrapper.OverflowAction = AsyncTargetWrapperOverflowAction.Grow;

            SimpleConfigurator.ConfigureForTargetLogging(wrapper, LogLevel.Debug);
        }

        private void serializer_UnknownNode(object sender, XmlNodeEventArgs e)
        {
            Console.WriteLine("Unknown Node:" + e.Name + "\t" + e.Text);
        }

        private void serializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            System.Xml.XmlAttribute attr = e.Attr;
            Console.WriteLine("Unknown attribute " +
            attr.Name + "='" + attr.Value + "'");
        }

        internal void LogConversionResult(string converting)
        {
            if (_logConversion)
            {
                string s = Convert.ToString(0 - _statusFlag, 2);
                char[] bits = s.PadLeft(17, '0').ToCharArray();
                Array.Reverse(bits);

                logger.Info("----------------------------");
                logger.Info("Converting: " + converting +  " (" + _statusFlag + ")");

                for (int i = 0; i < 17; i++)
                {
                    if (bits[i] == '1')
                    {
                        logger.Info(_statusMessages[i]);
                    }
                }

                logger.Info("----------------------------");
            }
        }

        internal void ResetStatusCode()
        {
            _statusFlag = 0;
        }

        internal int StatusCode
        {
            get
            {
                return _statusFlag;
            }
        }

        internal JMSMLConfig ConfigData
        {
            get
            {
                return _configData;
            }
        }

        internal Library Library 
        {
            get
            {
                return this._library;
            }
        }

        internal List<SymbolSet> SymbolSets
        {
            get
            {
                return new List<SymbolSet>(_sortedSymbolSets.Values);
                //return this._symbolSets;
            }
        }

        internal string Graphics
        {
            get
            {
                return "TODO";
            }
        }

        internal LibraryAmplifier SymbolAmplifier(string id)
        {
            LibraryAmplifier retObj = null;

            foreach (LibraryAmplifier lObj in this._library.Amplifiers)
            {
                if (lObj.ID == id)
                {
                    return lObj;
                }
            }

            logger.Warn("Label " + id + " was not found in the base library file.");

            return retObj;
        }

        internal LibraryDrawRule DrawRule(string id)
        {
            LibraryDrawRule retObj = null;

            foreach (LibraryDrawRule lObj in this._library.DrawRules)
            {
                if (lObj.ID == id)
                {
                    return lObj;
                }
            }

            logger.Warn("DrawRule " + id + " was not found in the base library file.");

            return retObj;
        }

        internal LibraryVersion Version(ushort codeOne, ushort codeTwo)
        {
            LibraryVersion retObj = null;

            foreach (LibraryVersion lObj in this._library.Versions)
            {
                if (lObj.VersionCode.DigitOne == codeOne &&
                    lObj.VersionCode.DigitTwo == codeTwo)
                {
                    return lObj;
                }
            }

            _statusFlag -= 1;

            return retObj;
        }

        internal LibraryContext Context(ushort code)
        {
            LibraryContext retObj = null;

            foreach (LibraryContext lObj in this._library.Contexts)
            {
                if (lObj.ContextCode == code)
                {
                    return lObj;
                }
            }

            _statusFlag -= 2;

            return retObj;
        }

        internal LibraryContext Context(string id)
        {
            LibraryContext retObj = null;

            foreach (LibraryContext lObj in this._library.Contexts)
            {
                if (lObj.ID == id)
                {
                    return lObj;
                }
            }

            _statusFlag -= 2;

            return retObj;
        }

        internal LibraryDimension Dimension(string id)
        {
            LibraryDimension retObj = null;

            foreach (LibraryDimension lObj in this._library.Dimensions)
            {
                if (lObj.ID == id)
                {
                    return lObj;
                }
            }

            _statusFlag -= 4;

            return retObj;
        }

        internal LibraryDimension DimensionBySymbolSet(string symbolSetID)
        {
            LibraryDimension retObj = null;

            foreach (LibraryDimension lObj in this._library.Dimensions)
            {
                foreach(LibraryDimensionSymbolSetRef ssRef in lObj.SymbolSets)
                {
                    if (ssRef.ID == symbolSetID)
                    {
                        return lObj;
                    }
                }
            }

            _statusFlag -= 4;

            return retObj;
        }

        internal LibraryDimension DimensionByLegacyCode(string code, string firstLetterInFunction = "", string codingSchemeLetter = "")
        {
            LibraryDimension retObj = null;

            foreach (LibraryDimension lObj in this._library.Dimensions)
            {
                if (lObj.LegacyDimensionCode != null)
                {
                    foreach (LegacyLetterCodeType lObj2 in lObj.LegacyDimensionCode)
                    {
                        if ((lObj2.Value == code && lObj2.FirstFunctionLetter == "" && lObj2.CodingSchemeLetter == "") ||
                            (lObj2.Value == code && lObj2.FirstFunctionLetter == firstLetterInFunction && lObj2.CodingSchemeLetter == "") ||
                            (lObj2.Value == code && lObj2.FirstFunctionLetter == "" && lObj2.CodingSchemeLetter == codingSchemeLetter) ||
                            (lObj2.Value == code && lObj2.FirstFunctionLetter == firstLetterInFunction && lObj2.CodingSchemeLetter == codingSchemeLetter))
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 4;

            return retObj;
        }

        internal LibraryStandardIdentity StandardIdentity(ushort code)
        {
            LibraryStandardIdentity retObj = null;

            foreach (LibraryStandardIdentity lObj in this._library.StandardIdentities)
            {
                if (lObj.StandardIdentityCode == code)
                {
                    return lObj;
                }
            }

            _statusFlag -= 8;

            return retObj;
        }

        internal LibraryStandardIdentity StandardIdentity(string id)
        {
            LibraryStandardIdentity retObj = null;

            foreach (LibraryStandardIdentity lObj in this._library.StandardIdentities)
            {
                if (lObj.ID == id)
                {
                    return lObj;
                }
            }

            _statusFlag -= 8;

            return retObj;
        }

        internal LibraryStandardIdentityGroup StandardIdentityGroup(string id)
        {
            LibraryStandardIdentityGroup retObj = null;

            foreach (LibraryStandardIdentityGroup lObj in this._library.StandardIdentityGroups)
            {
                if (lObj.ID == id)
                {
                    return lObj;
                }
            }

            return retObj;
        }

        internal LibraryStandardIdentityGroup StandardIdentityGroup(LibraryStandardIdentity standardIdentity)
        {
            LibraryStandardIdentityGroup retObj = null;

            foreach (LibraryStandardIdentityGroup lObj in this._library.StandardIdentityGroups)
            {
                foreach (string id in lObj.StandardIdentityIDs.Split(' '))
                {
                    if (standardIdentity.ID == id)
                    {
                        return lObj;
                    }
                }
            }

            return retObj;
        }

        internal SymbolSet SymbolSet(ushort symbolSetCodeOne, ushort symbolSetCodeTwo)
        {
            SymbolSet retObj = null;

            foreach (SymbolSet lObj in this._symbolSets)
            {
                if (lObj.SymbolSetCode.DigitOne == symbolSetCodeOne &&
                    lObj.SymbolSetCode.DigitTwo == symbolSetCodeTwo)
                {
                    return lObj;
                }
            }

            _statusFlag -= 16;

            return retObj;
        }

        internal SymbolSet SymbolSet(string symbolSetID)
        {
            SymbolSet retObj = null;

            foreach (SymbolSet ss in _symbolSets)
            {
                if (ss.ID == symbolSetID)
                {
                    retObj = ss;
                    break;
                }
            }

            return retObj;
        }

        internal SymbolSet SymbolSet(string dimensionID, string legacySIDC)
        {
            SymbolSet retObj = null;

            string code = legacySIDC.Substring(4, 6);
            string schemaCode = legacySIDC.Substring(0, 1);
            string dimensionCode = legacySIDC.Substring(2, 1);

            foreach (SymbolSet lObj in this._symbolSets)
            {
                // TODO: Following needs to be fixed for the Cyberspace symbol set

                if (lObj.DimensionID == dimensionID)
                {
                    if (lObj.LegacySymbols != null)
                    {
                        foreach (SymbolSetLegacySymbol lObj2 in lObj.LegacySymbols)
                        {
                            foreach (LegacyFunctionCodeType lObj3 in lObj2.LegacyFunctionCode)
                            {
                                if ((lObj3.Value == code && lObj3.SchemaOverride == "" && lObj3.DimensionOverride == "") ||
                                    (lObj3.Value == code && lObj3.SchemaOverride == "" && lObj3.DimensionOverride == dimensionCode) ||
                                    (lObj3.Value == code && lObj3.SchemaOverride == schemaCode && lObj3.DimensionOverride == "") ||
                                    (lObj3.Value == code && lObj3.SchemaOverride == schemaCode && lObj3.DimensionOverride == dimensionCode))
                                {
                                    return lObj;
                                }
                            }
                        }
                    }
                }
            }

            _statusFlag -= 16;

            return retObj;
        }

        internal LibraryStatus Status(ushort code)
        {
            LibraryStatus retObj = null;

            foreach (LibraryStatus lObj in this._library.Statuses)
            {
                if (lObj.StatusCode == code)
                {
                    return lObj;
                }
            }

            _statusFlag -= 32;

            return retObj;
        }

        internal LibraryStatus Status(string code, string codingScheme)
        {
            LibraryStatus retObj = this._library.Statuses[0];

            foreach (LibraryStatus lObj in this._library.Statuses)
            {
                if (lObj.LegacyStatusCode != null)
                {
                    foreach (LegacyLetterCodeType lObj2 in lObj.LegacyStatusCode)
                    {
                        if ((lObj2.Value == code && lObj2.CodingSchemeLetter == "") ||
                            (lObj2.Value == code && lObj2.CodingSchemeLetter == codingScheme))
                        {
                            return lObj;
                        }
                    }
                }
            }

            //_statusFlag -= 32;

            return retObj;
        }

        internal LibraryHQTFDummy HQTFDummy(ushort code)
        {
            LibraryHQTFDummy retObj = null;

            foreach (LibraryHQTFDummy lObj in this._library.HQTFDummies)
            {
                if (lObj.HQTFDummyCode == code)
                {
                    return lObj;
                }
            }

            _statusFlag -= 64;

            return retObj;
        }

        internal LibraryHQTFDummy HQTFDummy(string code)
        {
            LibraryHQTFDummy retObj = null;

            foreach (LibraryHQTFDummy lObj in this._library.HQTFDummies)
            {
                if (lObj.LegacyHQTFDummyCode != null)
                {
                    foreach (LegacyLetterCodeType lObj2 in lObj.LegacyHQTFDummyCode)
                    {
                        if (lObj2.Value == code)
                        {
                            return lObj;
                        }
                    }
                }
            }

            if(retObj == null)
            {
                retObj = this.HQTFDummy(0);
            }

            return retObj;
        }

        internal LibraryAmplifierGroup AmplifierGroup(ushort code)
        {
            LibraryAmplifierGroup retObj = null;

            foreach (LibraryAmplifierGroup lObj in this._library.AmplifierGroups)
            {
                if (lObj.AmplifierGroupCode == code)
                {

                    return lObj;
                }
            }

            _statusFlag -= 128;

            return retObj;
        }

        internal LibraryAmplifierGroup AmplifierGroup(LibraryAmplifierGroupAmplifier amplifier)
        {
            LibraryAmplifierGroup retObj = null;

            foreach (LibraryAmplifierGroup lObj in this._library.AmplifierGroups)
            {
                foreach (LibraryAmplifierGroupAmplifier lObj2 in lObj.Amplifiers)
                {
                    if (lObj2.Equals(amplifier))
                    {
                        return lObj;
                    }
                }
            }

            _statusFlag -= 128;

            return retObj;
        }

        internal LibraryAmplifierGroupAmplifier Amplifier(LibraryAmplifierGroup group, ushort code)
        {
            LibraryAmplifierGroupAmplifier retObj = null;

            if (group != null)
            {
                foreach (LibraryAmplifierGroupAmplifier lObj in group.Amplifiers)
                {
                    if (lObj.AmplifierCode == code)
                    {
                        return lObj;
                    }
                }
            }

            _statusFlag -= 256;

            return retObj;
        }

        internal LibraryAmplifierGroupAmplifier Amplifier(ushort groupCode, ushort code)
        {
            LibraryAmplifierGroup group = this.AmplifierGroup(groupCode);

            LibraryAmplifierGroupAmplifier retObj = this.Amplifier(group, code);

            return retObj;
        }

        internal LibraryAmplifierGroupAmplifier Amplifier(string code, string codingScheme)
        {
            LibraryAmplifierGroupAmplifier retObj = null;

            foreach (LibraryAmplifierGroup lObj in this._library.AmplifierGroups)
            {
                if (lObj.Amplifiers != null)
                {
                    foreach (LibraryAmplifierGroupAmplifier lObj2 in lObj.Amplifiers)
                    {
                        if (lObj2.LegacyModifierCode != null)
                        {
                            foreach (LegacyLetterCodeType lObj3 in lObj2.LegacyModifierCode)
                            {
                                if ((lObj3.Value == code && lObj3.CodingSchemeLetter == "") ||
                                    (lObj3.Value == code && lObj3.CodingSchemeLetter == codingScheme))
                                {
                                    return lObj2;
                                }
                            }
                        }
                    }
                }
            }

           retObj = Amplifier("-", codingScheme);

            return retObj;
        }

        internal LibraryAffiliation Affiliation(string contextID, string dimensionID, string standardIdentityID)
        {
            LibraryAffiliation retObj = null;

            foreach (LibraryAffiliation lObj in this._library.Affiliations)
            {
                if (lObj.ContextID == contextID && lObj.DimensionID == dimensionID && lObj.StandardIdentityID == standardIdentityID)
                {
                    return lObj;
                }
            }

            _statusFlag -= 512;

            return retObj;
        }

        internal LibraryAffiliation AffiliationByLegacyCode(string legacyStandardIdentityCode, string dimensionID)
        {
            LibraryAffiliation retObj = null;

            // For each affiliation (combination of context, dimension, and standard identity)

            foreach (LibraryAffiliation lObj in this._library.Affiliations)
            {
                if (lObj.DimensionID == dimensionID && lObj.LegacyStandardIdentityCode != null)
                {
                    // For each standard identity

                    foreach (LegacyLetterCodeType lObj2 in lObj.LegacyStandardIdentityCode)
                    {
                        // If this is the standard identity we are looking for...

                        if (lObj2.Value == legacyStandardIdentityCode)
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 512;

            return retObj;
        }

        internal LibraryAffiliation AffiliationByLegacyCode(string legacyStandardIdentityCode, string legacyDimensionCode, string legacyFirstLetterInFunction, string legacyCodingSchemeLetter)
        {
            LibraryAffiliation retObj = null;

            // For each affiliation (combination of context, dimension, and standard identity)

            foreach(LibraryAffiliation lObj in this._library.Affiliations)
            {
                if (lObj.LegacyStandardIdentityCode != null)
                {
                    // For each standard identity

                    foreach (LegacyLetterCodeType lObj2 in lObj.LegacyStandardIdentityCode)
                    {
                        // If this is the standard identity we are looking for...

                        if (lObj2.Value == legacyStandardIdentityCode)
                        {
                            // Grab the dimension associated with the current affiliation

                            LibraryDimension lDim = this.Dimension(lObj.DimensionID);
                            
                            // For each dimension code in that dimension...

                            foreach (LegacyLetterCodeType lObj3 in lDim.LegacyDimensionCode)
                            {
                                // Test to see if this is the dimension we are looking for by checking to see if the codes match and there is no
                                // first function letter and coding schema letter override, or...
                                // use the overriding first function letter to refine the test, or...
                                // use the overriding coding scheme letter to refine the test.

                                if((lObj3.Value == legacyDimensionCode && lObj3.FirstFunctionLetter == "" && lObj3.CodingSchemeLetter == "") ||
                                   (lObj3.Value == legacyDimensionCode && lObj3.FirstFunctionLetter == legacyFirstLetterInFunction && lObj3.CodingSchemeLetter == "") ||
                                   (lObj3.Value == legacyDimensionCode && lObj3.FirstFunctionLetter == ""  && lObj3.CodingSchemeLetter == legacyCodingSchemeLetter) ||
                                   (lObj3.Value == legacyDimensionCode && lObj3.FirstFunctionLetter == legacyFirstLetterInFunction && lObj3.CodingSchemeLetter == legacyCodingSchemeLetter))

                                    // We have a match, so return the current affiliation

                                    return lObj;
                            }
                        }
                    }
                }
            }

            _statusFlag -= 512;

            return retObj;
        }

        internal SymbolSetEntity Entity(SymbolSet symbolSet, ushort entityCodeOne, ushort entityCodeTwo)
        {
            SymbolSetEntity retObj = null;

            if (symbolSet != null)
            {
                if (symbolSet.Entities != null)
                {
                    foreach (SymbolSetEntity lObj in symbolSet.Entities)
                    {
                        if (lObj.EntityCode.DigitOne == entityCodeOne &&
                            lObj.EntityCode.DigitTwo == entityCodeTwo)
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 2048;

            return retObj;
        }

        internal SymbolSetEntity Entity(SymbolSet symbolSet, string entityID)
        {
            SymbolSetEntity retObj = null;

            if (symbolSet != null)
            {
                if (symbolSet.Entities != null)
                {
                    foreach (SymbolSetEntity lObj in symbolSet.Entities)
                    {
                        if (lObj.ID == entityID)
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 2048;

            return retObj;
        }

        internal SymbolSetEntityEntityType EntityType(SymbolSetEntity entity, ushort entityTypeCodeOne, ushort entityTypeCodeTwo)
        {
            SymbolSetEntityEntityType retObj = null;

            if (entity != null)
            {
                if (entity.EntityTypes != null)
                {
                    foreach (SymbolSetEntityEntityType lObj in entity.EntityTypes)
                    {
                        if (lObj.EntityTypeCode.DigitOne == entityTypeCodeOne &&
                            lObj.EntityTypeCode.DigitTwo == entityTypeCodeTwo)
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 4096;

            return retObj;
        }

        internal SymbolSetEntityEntityType EntityType(SymbolSetEntity entity, string entityTypeID)
        {
            SymbolSetEntityEntityType retObj = null;

            if (entity != null)
            {
                if (entity.EntityTypes != null)
                {
                    foreach (SymbolSetEntityEntityType lObj in entity.EntityTypes)
                    {
                        if (lObj.ID == entityTypeID)
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 4096;

            return retObj;
        }

        internal EntitySubTypeType EntitySubType(SymbolSetEntityEntityType entityType, ushort entitySubTypeCodeOne, ushort entitySubTypeCodeTwo)
        {
            EntitySubTypeType retObj = null;

            if (entityType != null)
            {
                if (entityType.EntitySubTypes != null)
                {
                    foreach (EntitySubTypeType lObj in entityType.EntitySubTypes)
                    {
                        if (lObj.EntitySubTypeCode.DigitOne == entitySubTypeCodeOne &&
                            lObj.EntitySubTypeCode.DigitTwo == entitySubTypeCodeTwo)
                        {
                            return lObj;
                        }
                    }
                }
            }

            _statusFlag -= 8192;

            return retObj;
        }

        internal EntitySubTypeType EntitySubType(SymbolSet ss, ushort entitySubTypeCodeOne, ushort entitySubTypeCodeTwo)
        {
            EntitySubTypeType retObj = null;

            if (ss != null)
            {
                if (ss.SpecialEntitySubTypes != null)
                {
                    foreach (EntitySubTypeType lObj in ss.SpecialEntitySubTypes)
                    {
                        if (lObj.EntitySubTypeCode.DigitOne == entitySubTypeCodeOne &&
                            lObj.EntitySubTypeCode.DigitTwo == entitySubTypeCodeTwo)
                        {
                            return lObj;
                        }
                    }
                }
            }

            return retObj;
        }

        internal EntitySubTypeType EntitySubType(SymbolSet ss, SymbolSetEntityEntityType entityType, string entitySubTypeID)
        {
            EntitySubTypeType retObj = null;

            if (entityType != null)
            {
                if (entityType.EntitySubTypes != null)
                {
                    foreach (EntitySubTypeType lObj in entityType.EntitySubTypes)
                    {
                        if (lObj.ID == entitySubTypeID)
                        {
                            return lObj;
                        }
                    }
                }

                // Can't find it.  Check to see if the symbol set has special entity subtypes

                if (ss != null)
                {
                    if (ss.SpecialEntitySubTypes != null)
                    {
                        foreach (EntitySubTypeType lObj in ss.SpecialEntitySubTypes)
                        {
                            if (lObj.ID == entitySubTypeID)
                            {
                                return lObj;
                            }
                        }
                    }
                }
            }

            _statusFlag -= 8192;

            return retObj;
        }

        internal ModifiersTypeModifier ModifierOne(SymbolSet symbolSet, ushort modifierCodeOne, ushort modifierCodeTwo)
        {
            ModifiersTypeModifier retObj = null;

            if (symbolSet != null)
                if (symbolSet.SectorOneModifiers != null)
                {
                    foreach (ModifiersTypeModifier lObj in symbolSet.SectorOneModifiers)
                    {
                        if (lObj.ModifierCode.DigitOne == modifierCodeOne &&
                           lObj.ModifierCode.DigitTwo == modifierCodeTwo)
                        {
                            return lObj;
                        }
                    }
                }

            _statusFlag -= 16384;

            return retObj;
        }

        internal ModifiersTypeModifier ModifierOne(SymbolSet symbolSet, string modifierID)
        {
            ModifiersTypeModifier retObj = null;

            if (symbolSet != null)
                if(symbolSet.SectorOneModifiers != null)
                {
                    foreach (ModifiersTypeModifier lObj in symbolSet.SectorOneModifiers)
                    {
                        if (lObj.ID == modifierID || modifierID == "NA" && lObj.ModifierCode.DigitOne == 0 && lObj.ModifierCode.DigitTwo == 0)
                        {
                            return lObj;
                        }
                    }
                }

            _statusFlag -= 16384;

            return retObj;
        }

        internal ModifiersTypeModifier ModifierTwo(SymbolSet symbolSet, ushort modifierCodeOne, ushort modifierCodeTwo)
        {
            ModifiersTypeModifier retObj = null;

            if (symbolSet != null)
                if(symbolSet.SectorTwoModifiers != null)
                {
                    foreach (ModifiersTypeModifier lObj in symbolSet.SectorTwoModifiers)
                    {
                        if (lObj.ModifierCode.DigitOne == modifierCodeOne &&
                           lObj.ModifierCode.DigitTwo == modifierCodeTwo)
                        {
                            return lObj;
                        }
                    }
                }

            _statusFlag -= 32768;

            return retObj;
        }

        internal ModifiersTypeModifier ModifierTwo(SymbolSet symbolSet, string modifierID)
        {
            ModifiersTypeModifier retObj = null;

            if (symbolSet != null)
                if(symbolSet.SectorTwoModifiers != null)
                {
                    foreach (ModifiersTypeModifier lObj in symbolSet.SectorTwoModifiers)
                    {
                        if (lObj.ID == modifierID || modifierID == "NA" && lObj.ModifierCode.DigitOne == 0 && lObj.ModifierCode.DigitTwo == 0)
                        {
                            return lObj;
                        }
                    }
                }

            _statusFlag -= 32768;

            return retObj;
        }

        internal SymbolSetLegacySymbol LegacySymbol(string legacySIDC, ref LibraryDimension dimensionOut, ref SymbolSet ssOut)
        {
            SymbolSetLegacySymbol retObj = null;

            dimensionOut = null;
            ssOut = null;

            string codingSchemeLetter = legacySIDC.Substring(0, 1);
            string code = legacySIDC.Substring(2, 1);
            string firstLetterInFunction = legacySIDC.Substring(4, 1);

            foreach (LibraryDimension lObj in _library.Dimensions)
            {
                if (lObj.LegacyDimensionCode != null)
                {
                    foreach (LegacyLetterCodeType lObj2 in lObj.LegacyDimensionCode)
                    {
                        if ((lObj2.Value == code && lObj2.FirstFunctionLetter == "" && lObj2.CodingSchemeLetter == "") ||
                            (lObj2.Value == code && lObj2.FirstFunctionLetter == firstLetterInFunction && lObj2.CodingSchemeLetter == "") ||
                            (lObj2.Value == code && lObj2.FirstFunctionLetter == "" && lObj2.CodingSchemeLetter == codingSchemeLetter) ||
                            (lObj2.Value == code && lObj2.FirstFunctionLetter == firstLetterInFunction && lObj2.CodingSchemeLetter == codingSchemeLetter))
                        {
                            if (lObj.SymbolSets != null)
                            {
                                foreach (LibraryDimensionSymbolSetRef ssRef in lObj.SymbolSets)
                                {
                                    SymbolSet ss = this.SymbolSet(ssRef.ID);

                                    if (ss != null)
                                    {
                                        retObj = this.LegacySymbol(ss, legacySIDC);

                                        if (retObj != null)
                                        {
                                            dimensionOut = lObj;
                                            ssOut = ss;
                                            
                                            return retObj;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                }
            }

            if (retObj == null)
            {
                _statusFlag -= 4;  // Can't find a Dimension
                _statusFlag -= 16; // Can't find a SymbolSet
                _statusFlag -= 65536; // Can't find a LegacySymbol
            }

            return retObj;
        }

        internal SymbolSetLegacySymbol LegacySymbol(SymbolSet symbolSet, string legacySIDC)
        {
            SymbolSetLegacySymbol retObj = null;

            string schema = legacySIDC.Substring(0, 1);
            string functionCode = legacySIDC.Substring(4, 6);
            string dimension = legacySIDC.Substring(2, 1);
            string hqtffd = legacySIDC.Substring(10, 1);
            string amplifier = legacySIDC.Substring(11, 1);
            string tail = legacySIDC.Substring(12, 3);

            if (symbolSet != null)
            {
                if (symbolSet.LegacySymbols != null)
                {
                    foreach (SymbolSetLegacySymbol lObj in symbolSet.LegacySymbols)
                    {
                        foreach (LegacyFunctionCodeType lObj2 in lObj.LegacyFunctionCode)
                        {
                            if (lObj2.Value == functionCode)
                            {
                                //
                                // Thanks to many exceptions in 2525C, given also that function codes are not unique, we need to check other legacy code values
                                //
                                if ((lObj2.SchemaOverride == "" || (lObj2.SchemaOverride != "" && lObj2.SchemaOverride == schema)) &&
                                    (lObj2.DimensionOverride == "" || (lObj2.DimensionOverride != "" && lObj2.DimensionOverride == dimension)) &&
                                    (lObj2.HQTFFDOverride == "" || (lObj2.HQTFFDOverride != "" && lObj2.HQTFFDOverride == hqtffd)) &&
                                    (lObj2.AmplifierOverride == "" || (lObj2.AmplifierOverride != "" && lObj2.AmplifierOverride == amplifier)) &&
                                    (lObj2.TailOverride == "" || (lObj2.TailOverride != "" && lObj2.TailOverride == tail)))
                                {
                                    return lObj;
                                }
                            }
                        }
                    }
                }
            }

            return retObj;
        }

        internal SymbolSetLegacySymbol LegacySymbol(SymbolSet symbolSet, 
                                                  SymbolSetEntity entity, 
                                                  SymbolSetEntityEntityType entityType, 
                                                  EntitySubTypeType entitySubType, 
                                                  ModifiersTypeModifier modifierOne, 
                                                  ModifiersTypeModifier modifierTwo)
        {
            SymbolSetLegacySymbol retObj = null;

            // Account now for non-null modifiers that are effectively treated like null (00)

            if (modifierOne != null)
            {
                if (modifierOne.ModifierCode.DigitOne == 0 &&
                    modifierOne.ModifierCode.DigitTwo == 0)

                    modifierOne = null;
            }

            if (modifierTwo != null)
            {
                if (modifierTwo.ModifierCode.DigitOne == 0 &&
                    modifierTwo.ModifierCode.DigitTwo == 0)

                    modifierTwo = null;
            }

            // Now build a legacy 15 character SIDC from the 2525D symbol objects

            if (symbolSet != null)
            {
                if (symbolSet.LegacySymbols != null)
                {
                    int match = 0;

                    foreach (SymbolSetLegacySymbol lObj in symbolSet.LegacySymbols)
                    {
                        if(entity != null)
                        {
                            if (lObj.EntityID != "NA")
                            {
                                if (lObj.EntityID == entity.ID)
                                    match++;
                            }
                        }
                        else if(lObj.EntityID == "NA")
                            match++;

                        if(entityType != null)
                        {
                            if (lObj.EntityTypeID != "NA")
                            {
                                if (lObj.EntityTypeID == entityType.ID)
                                    match++;
                            }
                        }
                        else if(lObj.EntityTypeID == "NA")
                            match++;

                        if(entitySubType != null)
                        {
                            if (lObj.EntitySubTypeID != "NA")
                            {
                                if (lObj.EntitySubTypeID == entitySubType.ID)
                                    match++;
                            }
                        }
                        else if(lObj.EntitySubTypeID == "NA")
                            match++;

                        if(modifierOne != null)
                        {
                            if (lObj.ModifierOneID != "NA")
                            {
                                if (lObj.ModifierOneID == modifierOne.ID)
                                    match++;
                            }
                        }
                        else if(lObj.ModifierOneID == "NA")
                            match++;

                        if(modifierTwo != null)
                        {
                            if (lObj.ModifierTwoID != "NA")
                            {
                                if (lObj.ModifierTwoID == modifierTwo.ID)
                                    match++;
                            }
                        }
                        else if(lObj.ModifierTwoID == "NA")
                            match++;

                        if(match == 5)
                        {
                            return lObj;
                        }
                        
                        match = 0;
                    }
                }
            }

            _statusFlag -= 65536;

            return retObj;
        }

        public Symbol InvalidSymbol
        {
            get
            {
                return _invalidSymbol;
            }
        }

        public Symbol RetiredSymbol
        {
            get
            {
                return _retiredSymbol;
            }
        }

        public bool IsLogging
        {
            get
            {
                return _logConversion;
            }

            set
            {
                _logConversion = value;
            }
        }

        public bool DrawColoredOCABars
        {
            get
            {
                return _drawColoredOCABars;
            }

            set
            {
                _drawColoredOCABars = value;
            }
        }

        public bool DrawCivilianFrames
        {
            get
            {
                return _drawCivilianFrames;
            }

            set
            {
                _drawCivilianFrames = value;
            }
        }

        public Symbol MakeSymbol(string legacyStandard, string legacySIDC)
        {
            Symbol s = null;

            if (legacySIDC.Length == 15)
            {
                legacyStandard = legacyStandard.ToUpper();

                legacySIDC = legacySIDC.Replace('*', '-');
                legacySIDC = legacySIDC.ToUpper();

                s = new Symbol(this, legacyStandard, legacySIDC, _drawColoredOCABars, _drawCivilianFrames);

                if (s.SymbolStatus == SymbolStatusEnum.statusEnumInvalid)
                {
                    logger.Warn("SIDC " + legacySIDC + " is an invalid symbol.");
                    s = null;
                }
            }
            else
            {
                logger.Error("SIDC " + legacySIDC + " is not 15 characters in length.");
            }

            return s;
        }

        public Symbol MakeSymbol(UInt32 partA, UInt32 partB)
        {
            SIDC sid = new SIDC(partA, partB);
            Symbol s = new Symbol(this, sid, _drawColoredOCABars, _drawCivilianFrames);

            if (s.SymbolStatus == SymbolStatusEnum.statusEnumInvalid)
            {
                s = null;
            }

            return s;
        }

        public Symbol MakeSymbol(SIDC sidc)
        {
            Symbol s = new Symbol(this, sidc, _drawColoredOCABars, _drawCivilianFrames);

            if (s.SymbolStatus == SymbolStatusEnum.statusEnumInvalid)
            {
                s = null;
            }

            return s;
        }
    }
}
