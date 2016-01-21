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
    public class DomainHQTFFDExport : HQTFFDExport, IHQTFFDExport
    {
        // Class designed to export HQTFFD elements as name and value information

        public DomainHQTFFDExport(ConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        string IHQTFFDExport.Headers
        {
            get { return "Name,Value"; }
        }

        string IHQTFFDExport.Line(LibraryHQTFDummy hqTFFD, LibraryHQTFDummyGraphic graphic)
        {
            //LibraryStandardIdentityGroup identityGroup = _configHelper.Librarian.StandardIdentityGroup(graphic.StandardIdentityGroup);
            //LibraryDimension dimension = _configHelper.Librarian.Dimension(graphic.Dimension);

            string result = BuildHQTFFDItemName(null, null, hqTFFD) + "," + BuildHQTFFDCode(null, null, hqTFFD);

            return result;
        }
    }
}
