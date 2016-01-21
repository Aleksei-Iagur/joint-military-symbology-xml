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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JointMilitarySymbologyLibrary
{
    class SimpleEntityExport : EntityExport, IEntityExport
    {
        // This class implements IEntityExport to export a SymbolSet and a given
        // entity within that SymbolSet.  The export format is very simple, breaking
        // down each part of the hierarchical nature of the entity structure and
        // documenting the Label attributes for each element in that "equation".

        public SimpleEntityExport(ConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        string IEntityExport.Headers
        {
            get { return "SymbolSet,Entity,EntityType,EntitySubType,StandardIdentity,Code,GeometryType,IconType,UniqueName"; }
        }

        string IEntityExport.Line(LibraryStandardIdentityGroup sig, SymbolSet ss, SymbolSetEntity e, SymbolSetEntityEntityType eType, EntitySubTypeType eSubType)
        {
            GeometryType geoType = GeometryType.POINT;
            IconType icoType = IconType.MAIN;

            string result = Convert.ToString(ss.SymbolSetCode.DigitOne) + Convert.ToString(ss.SymbolSetCode.DigitTwo);
            
            string code = BuildEntityCode(sig, ss, e, eType, eSubType);

            // Remove the first two characters as this output does not require the symbol set code, and exclude any trailing underbar digit
            // Note that these code will not be unique, with the substring operation in place.

            code = code.Substring(2,6);
            
            result = result + ",";

            result = result + e.Label.Replace(',', '-');
            geoType = e.GeometryType;
            icoType = e.Icon;

            result = result + ",";

            if (eType != null)
            {
                result = result + eType.Label.Replace(',', '-');
                geoType = eType.GeometryType;
                icoType = eType.Icon;
            }

            result = result + ",";

            if (eSubType != null)
            {
                result = result + eSubType.Label.Replace(',', '-');
                geoType = eSubType.GeometryType;
                icoType = eSubType.Icon;
            }

            if (sig != null)
            {
                result = result + "," + sig.Label;
            }
            else
            {
                result = result + ",";
            }

            result = result + "," + code + "," + _geometryList[(int)geoType];

            result = result + "," + Convert.ToString(icoType) + "," + BuildEntityItemName(sig, ss, e, eType, eSubType); 

            return result;
        }

        string IEntityExport.Line(LibraryStandardIdentityGroup sig, SymbolSet ss, EntitySubTypeType eSubType)
        {
            // Dealing with a special entity sub type

            GeometryType geoType = GeometryType.POINT;
            IconType icoType = IconType.FULL_FRAME;

            string result = Convert.ToString(ss.SymbolSetCode.DigitOne) + Convert.ToString(ss.SymbolSetCode.DigitTwo);
            string code = BuildEntityCode(sig, ss, null, null, eSubType);

            // Remove the first two characters as this output does not require the symbol set code, and exclude any trailing underbar digit
            // Note that these code will not be unique, with the substring operation in place.

            code = code.Substring(2, 6);

            result = result + "," + eSubType.EntityCode + "," + eSubType.EntityTypeCode + "," + eSubType.Label.Replace(',', '-');
            geoType = eSubType.GeometryType;
            icoType = eSubType.Icon;

            if (sig != null)
            {
                result = result + "," + sig.Label;
            }
            else
            {
                result = result + ",";
            }

            result = result + "," + code + "," + _geometryList[(int)geoType];

            result = result + "," + Convert.ToString(icoType) + "," + BuildEntityItemName(sig, ss, null, null, eSubType); 

            return result;
        }

        string IEntityExport.Line(EntitySubTypeType eSubType)
        {
            return "";
        }
    }
}
