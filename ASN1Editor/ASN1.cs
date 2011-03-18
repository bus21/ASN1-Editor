﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ASN1Editor
{
    public static class ASN1
    {
        /// <summary>
        /// ASN1 classes
        /// </summary>
        public enum Class
        {
            Universal = 0,
            Application = 1,
            ContextSpecific = 2,
            Private = 3
        }

        /// <summary>
        /// Universal primitive ASN1 tag types
        /// </summary>
        public enum TagNumber
        {
            NonePrimitive = -1,
            Boolean = 1,
            Integer = 2,
            BitString = 3,
            OctetString = 4,
            Null = 5,
            ObjectIdentifier = 6,
            ObjectDescriptor = 7,
            InstanceOf = 8,
            Real = 9,
            Enumerated = 10,
            EmbeddedPdv = 11,
            Utrf8String = 12,
            RelativeOid = 13,
            Sequence = 16,
            Set = 17,
            NumericString = 18,
            PrintableString = 19,
            TeletextString = 20,
            VideotexString = 21,
            Ia5String = 22,
            UtcTime = 23,
            GeneralizedTime = 24,
            GraphicString = 25,
            VisibleString = 26,
            GeneralString = 27,
            UniversalString = 28,
            CharacterString = 29,
            BmpString = 30
        }

        /// <summary>
        /// Value used to indicate indefinite length in a tag's length field
        /// </summary>
        public const int IndefiniteLength = -2;

        /// <summary>
        /// End of Contents tag identifier (as specefied in X.690)
        /// </summary>
        public const int EocIdentifier = 0;

        /// <summary>
        /// End of Contents tag length (as specefied in X.690)
        /// </summary>
        public const int EocLength = 0;

        /// <summary>
        /// Decode ASN1 tags from a stream.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <returns>The root node read from the stream.</returns>
        public static ASN1Tag Decode(Stream stream)
        {
            return Decode(stream, false, true);
        }

        /// <summary>
        /// Decode ASN1 tags from a stream.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        /// <param name="singleNode">If true, read only one single node without subnodes.</param>
        /// <param name="readSubAsn1">If true, possible ASN1 data in BitStrings will be decoded.</param>
        /// <returns>The root node read from the stream.</returns>
        public static ASN1Tag Decode(Stream stream, bool singleNode, bool readSubAsn1)
        {
            ASN1Tag node = new ASN1Tag();

            ASN1.Class nodeClass;
            bool constructed;

            node.StartByte = stream.Position;
            node.Identifier = ReadIdentifier(stream, out nodeClass, out constructed);
            node.Class = nodeClass;
            node.Constructed = constructed;
            node.Length = ReadLength(stream);

            if (node.Constructed)
            {
                if (!singleNode)
                {
                    while ((stream.Position < node.StartByte + node.Length) || (node.Length == IndefiniteLength))
                    {
                        ASN1Tag subTag = Decode(stream, singleNode, readSubAsn1);
                        node.AddSubTag(subTag);
                        if (subTag.Identifier == EocIdentifier && subTag.Length == EocLength)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                ReadData(node, stream);

                if (readSubAsn1 && node.Identifier == (int)ASN1.TagNumber.BitString)
                {
                    ReadSubAsn1(node);
                }
            }

            return node;
        }

        /// <summary>
        /// Parse data in a (BitString) node if it's valid ASN1 and add read nodes as sub nodes to this node.
        /// </summary>
        /// <param name="node">Node to read data from.</param>
        /// <returns>True if valid ASN1 was read, false if not.</returns>
        private static bool ReadSubAsn1(ASN1Tag node)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(node.Data, 1, node.Data.Length - 1))
                {
                    ASN1Tag subTag = ASN1.Decode(ms);
                    subTag.ToShortText(); // To validate, if it fails with exception, it's not added

                    node.AddSubTag(subTag);
                    return true;
                }
            }
            catch (Exception) // TODO: narrow this down
            {
                return false;
            }
        }

        /// <summary>
        /// Read an ASN1 tag identifier from a stream (as specified in X.690)
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="tagClass">Tag class of the tag</param>
        /// <param name="constructed">Indicates if the tag is constructed = true (or primitive = false)</param>
        /// <returns></returns>
        private static int ReadIdentifier(Stream stream, out ASN1.Class tagClass, out bool constructed)
        {
            int identifier;
            int b = stream.ReadByte();
            if (b < 0) throw new IOException("Unexpected end of ASN.1 data while reading identifier.");

            //  8  7 | 6 | 5  4  3  2  1
            // Class |P/C|   Tag number
            tagClass = (ASN1.Class)(b >> 6);
            constructed = (b & 0x20) != 0;

            if ((b & 0x1f) != 0x1f)
            {
                // Single byte identifier
                identifier = b & 0x1f;
            }
            else
            {
                // Multi byte identifier
                identifier = 0;
                do
                {
                    b = stream.ReadByte();
                    if (b < 0) throw new IOException("Unexpected end of ASN.1 data while reading multi byte identifier.");

                    identifier <<= 7; // also happens first time, but that's ok, it's still 0
                    identifier |= b & 0x7F;

                    if (identifier == 0) throw new IOException("Invalid ASN.1 identifier (0 while reading constructed identifier).");
                } while ((b & 0x80) != 0);
            }

            return identifier;
        }

        /// <summary>
        /// Read an ASN1 tag's length from a stream (as specified in X.690)
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>Tag's length</returns>
        private static long ReadLength(Stream stream)
        {
            long length = 0;
            int b = stream.ReadByte();

            if ((b & 0x80) == 0)
            {
                // Short form length
                length = b & 0x7F;
            }
            else
            {
                // Long form length
                if ((b ^ 0x80) == 0)
                {
                    // indefinte length; terminated by EOC
                   length = IndefiniteLength;
                }
                else
                {
                    byte[] l_bytes = new byte[(int)(b ^ 0x80)];
                    if (l_bytes.Length > 8)
                    {
                        throw new IOException("Can't handle length field of more than 8 bytes.");
                    }

                    int read = stream.Read(l_bytes, 0, l_bytes.Length);
                    if (read != l_bytes.Length)
                    {
                        throw new IOException("Not enough bytes to read length.");
                    }

                    length = 0;
                    for (int i = 0; i < l_bytes.Length; ++i)
                    {
                        length <<= 8;
                        length |= l_bytes[i];
                    }
                }
            }

            return length;
        }

        private static void ReadData(ASN1Tag node, Stream stream)
        {
            if (node.Length > int.MaxValue)
            {
                throw new IOException("Can't read primitive data with more than " + int.MaxValue + " bytes.");
            }

            node.Data = new byte[(int)node.Length];
            int read = stream.Read(node.Data, 0, node.Data.Length);
            if (read != node.Data.Length)
            {
                throw new IOException("Unable to read enough data bytes.");
            }
        }

        internal static string GetUTNDescription(int identifier)
        {
            switch (identifier)
            {
                case 1: return "BOOLEAN";
                case 2: return "INTEGER";
                case 3: return "BIT_STRING";
                case 4: return "OCTET_STRING";
                case 5: return "NULL";
                case 6: return "OBJECT_IDENTIFIER";
                case 7: return "OBJECTDESCRIPTOR";
                case 8: return "INSTANCE_OF";
                case 9: return "REAL";
                case 10: return "ENUMERATED";
                case 11: return "EMBEDDED_PDV";
                case 12: return "UTF8STRING";
                case 13: return "RELATIVE_OID";
                case 16: return "SEQUENCE";
                case 17: return "SET";
                case 18: return "NUMERICSTRING";
                case 19: return "PRINTABLESTRING";
                case 20: return "TELETEXSTRING";
                case 21: return "VIDEOTEXSTRING";
                case 22: return "IA5STRING";
                case 23: return "UTCTIME";
                case 24: return "GENERALIZEDTIME";
                case 25: return "GRAPHICSTRING";
                case 26: return "VISIBLESTRING";
                case 27: return "GENERALSTRING";
                case 28: return "UNIVERSALSTRING";
                case 29: return "CHARACTER_STRING";
                case 30: return "BMPSTRING";
                default: return "UNKNOWN";
            }
        }
    }
}