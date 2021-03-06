﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Asn1Lib;

namespace Asn1Editor
{
    public class Asn1TreeNode : TreeNode
    {
        public Asn1Tag Asn1Node { get; private set; }

        public Asn1TreeNode(Asn1Tag asn1Node)
        {
            this.Asn1Node = asn1Node;

            // Use data length instead of full byte count
            this.Text = string.Concat("(", asn1Node.StartByte, ", ", (asn1Node.IndefiniteLength ? "inf" : Asn1.GetTotalByteCount(asn1Node).ToString()), ") ", asn1Node.ShortDescription);
            if (!asn1Node.Constructed)
            {
                this.Text += string.Concat(": ", asn1Node.DataText);
            }
            if (asn1Node.Class == Asn1.Class.Universal && asn1Node.Identifier <= 31)
            {
                this.ImageIndex = Asn1ImageIndexes[asn1Node.Identifier];
            }
            else
            {
                this.ImageIndex = 32;
            }
            this.SelectedImageIndex = this.ImageIndex;

            this.ContextMenu = CreateContextMenu();
        }

        protected ContextMenu CreateContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            MenuItem mnuDumpNode = menu.MenuItems.Add("Save node as...", (s, e) => Dump(SaveType.Node));
            MenuItem mnuDumpData = menu.MenuItems.Add("Save data as...", (s, e) => Dump(SaveType.Data));
            MenuItem mnuDumpText = menu.MenuItems.Add("Save text as...", (s, e) => Dump(SaveType.Text));
            return menu;
        }

        private enum SaveType
        {
            Node,
            Data,
            Text
        }

        private void Dump(SaveType dumpType)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            if (DialogResult.OK == fileDialog.ShowDialog())
            {
                switch (dumpType)
                {
                    case SaveType.Node:
                        Asn1.Encode(fileDialog.FileName, this.Asn1Node);
                        break;
                    case SaveType.Data:
                        Asn1.EncodeData(fileDialog.FileName, this.Asn1Node);
                        break;
                    case SaveType.Text:
                        Asn1.ExportText(fileDialog.FileName, this.Asn1Node);
                        break;
                    default:
                        break;
                }
            }
        }

        private int[] Asn1ImageIndexes = new int[] {
            32, // 0, TODO: look for best icon
            21, // Boolean = 1,
             1, // Integer = 2,
            23, // BitString = 3,
            25, // OctetString = 4,
            18, // Null = 5,
            11, // ObjectIdentifier = 6,
            11, // ObjectDescriptor = 7,
             6, // InstanceOf = 8,
             1, // Real = 9,
             1, // Enumerated = 10,
             1, // EmbeddedPdv = 11, TODO: look for best icon
             7, // Utrf8String = 12,
            35, // RelativeOid = 13,
             0, // 14, TODO: look for best icon
             0, // 15, TODO: look for best icon
            32, // Sequence = 16,
            33, // Set = 17,
             8, // NumericString = 18,
             7, // PrintableString = 19,
             7, // TeletextString = 20,
             7, // VideotexString = 21,
             7, // Ia5String = 22,
            34, // UtcTime = 23,
            34, // GeneralizedTime = 24,
             7, // GraphicString = 25,
             7, // VisibleString = 26,
             7, // GeneralString = 27,
             7, // UniversalString = 28,
             7, // CharacterString = 29,
             7, // BmpString = 30
            24, // Context specific = 31
            32, // Root = 32
        };

    }
}
