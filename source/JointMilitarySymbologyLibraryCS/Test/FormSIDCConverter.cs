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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Windows.Forms;
using System.IO;
using JointMilitarySymbologyLibrary;

namespace Test
{
    public partial class FormSIDCConverter : Form
    {
        private Librarian _librarian;
        private Symbol _symbol;
        private Symbol _badSymbol;
        private char[] _trimDash = new char[]{'-','*',' '};

        public FormSIDCConverter()
        {
            InitializeComponent();

            _librarian = new Librarian();
            _librarian.IsLogging = true;
            
            _badSymbol = _librarian.MakeSymbol(1001980000, 1000000000);
            _symbol = _badSymbol;
            updateControls();
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
    
        // Update controls on the form

        private void updateC(string s)
        {
            text2525C.Text = s;
        }

        private void updateD(string s1, string s2)
        {
            text2525D_1.Text = s1;
            text2525D_2.Text = s2;
        }

        private void updateFieldList()
        {
            listView1.Items.Clear();
            List<Dictionary<string, string>> labels = _symbol.Labels;
            foreach (Dictionary<string, string> label in labels)
            {
                ListViewItem item = new ListViewItem(label["Name"]);
                item.SubItems.Add(label["Label"]);
                item.SubItems.Add(label["Description"]);
                item.SubItems.Add(label["Remarks"]);
                item.SubItems.Add(label["X"]);
                item.SubItems.Add(label["Y"]);
                item.SubItems.Add(label["Type"]);
                item.SubItems.Add(label["Length"]);
                listView1.Items.Add(item);
            }
        }

        private void updateDrawRuleList()
        {
            listView2.Items.Clear();
            Dictionary<string, string> rule = _symbol.DrawRule;

            if (rule != null)
            {
                if (rule.Count > 0)
                {
                    ListViewItem item = new ListViewItem(rule["Name"]);
                    item.SubItems.Add(rule["AnchorPoints"]);
                    item.SubItems.Add(rule["SizeShape"]);
                    item.SubItems.Add(rule["Orientation"]);
                    listView2.Items.Add(item);
                }
            }

            label10.Text = _symbol.DrawNote;
        }

        private void updateGraphic()
        {
            // Change padding for control measures

            if (_symbol.SIDC.SymbolSetCode != "25" &&
                _symbol.SIDC.SymbolSetCode != "45" &&
                _symbol.SIDC.SymbolSetCode != "46" &&
                _symbol.SIDC.SymbolSetCode != "47")
                pictureBox1.Padding = new Padding(40,0,0,0);
            else
                pictureBox1.Padding = new Padding(15,0,0,0);

            pictureBox1.Image = _symbol.Bitmap(pictureBox1.Width, pictureBox1.Height);
        }

        private void updateControls()
        {
            if (_symbol != null)
            {
                updateC(_symbol.LegacySIDC);
                updateD(_symbol.SIDC.PartAString, _symbol.SIDC.PartBString);

                switch (_symbol.SymbolStatus)
                {
                    case SymbolStatusEnum.statusEnumNew:
                        toolStripStatusLabel1.Text = "Symbol is new/introduced in 2525D";
                        break;
                    case SymbolStatusEnum.statusEnumOld:
                        toolStripStatusLabel1.Text = "Symbol is old (in 2525C) and in 2525D";
                        break;
                    case SymbolStatusEnum.statusEnumRetired:
                        toolStripStatusLabel1.Text = "Symbol has been retired from 2525";
                        break;
                }

                TagsLabel.Text = _symbol.Tags;
                GeoLabel.Text = _symbol.GeometryType.ToString();

                updateFieldList();
                updateDrawRuleList();
                updateGraphic();
            }
            else
            {
                updateC("");
                updateD("", "");

                toolStripStatusLabel1.Text = "Symbol is invalid or not found in the symbol library";

                TagsLabel.Text = "";
                GeoLabel.Text = "";

                listView1.Items.Clear();
                listView2.Items.Clear();

                pictureBox1.Image = _badSymbol.Bitmap(pictureBox1.Width, pictureBox1.Height);
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string s = listBox1.SelectedItem.ToString();

            string[] l = s.Split('\t');

            _symbol = _librarian.MakeSymbol("2525C", l[0]);

            updateControls();
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

            string s = listBox2.SelectedItem.ToString();

            string[] l = s.Split('\t');
            string[] ll = l[0].Split(',');

            _symbol = _librarian.MakeSymbol(new SIDC(ll[0],ll[1]));

            updateControls();
        }

        private void text2525C_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                _symbol = _librarian.MakeSymbol("2525C", text2525C.Text);

                updateControls();
            }
            else
            {
                TextBox Box = (sender as TextBox);
                if (Box.SelectionStart < Box.TextLength && !Char.IsControl(e.KeyChar))
                {
                    int CacheSelectionStart = Box.SelectionStart; //Cache SelectionStart as its reset when the Text property of the TextBox is set.
                    StringBuilder sb = new StringBuilder(Box.Text); //Create a StringBuilder as Strings are immutable
                    sb[Box.SelectionStart] = e.KeyChar; //Add the pressed key at the right position
                    Box.Text = sb.ToString(); //SelectionStart is reset after setting the text, so restore it
                    Box.SelectionStart = CacheSelectionStart + 1; //Advance to the next char
                }
            }
        }

        private void text2525D_1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                _symbol = _librarian.MakeSymbol(new SIDC(text2525D_1.Text, text2525D_2.Text));

                updateControls();
            }
        }

        private void text2525D_2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                _symbol = _librarian.MakeSymbol(new SIDC(text2525D_1.Text, text2525D_2.Text));

                updateControls();
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            _librarian.DrawColoredOCABars = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            _librarian.DrawCivilianFrames = checkBox2.Checked;
        }

        private void text2525C_KeyUp(object sender, KeyEventArgs e)
        {
            string sidc = text2525C.Text;
            int start = text2525C.SelectionStart;
            int length = text2525C.SelectionLength;

            sidc = sidc.Replace('*', '-');
            sidc = sidc.TrimEnd(_trimDash);
            sidc = sidc.PadRight(15, '-');
            
            text2525C.Text = sidc.ToUpper();
            text2525C.SelectionStart = start;
            text2525C.SelectionLength = length;
        }
    }
}
