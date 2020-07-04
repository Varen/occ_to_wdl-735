using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Wdl_To_Occ_735
{



    class Program
    {
        // How much we lower the WDL to make it into an OCC
        

        static void Abort(string s)
        {
            Console.WriteLine("Error: " + s + ", Aborting..");
            Console.ReadLine();
            Environment.Exit(1);
        }
        static void Main(string[] args)
        {
            string s = "";
            if (args.Length == 0)
            {
                Console.Write("Specify Wdl Path: ");
                s = Console.ReadLine();
            }
            else
            {
                s = args[0];
            }
            if (string.IsNullOrEmpty(s))
                Abort("Must specify filename!");

            Console.Write("How much to offset the WDL? (25 is generally ok):");
            string occOffset = Console.ReadLine();
            short ValueToLowerBy;
            
            while(!short.TryParse(occOffset, out ValueToLowerBy))
            {
                Console.WriteLine("Invalid amount!");
                Console.Write("How much to offset the WDL? (25 is generally ok):");
                occOffset = Console.ReadLine();
            }
            

            // From WDL, same order as WDL.
            List<short[]> MapHeightData = new List<short[]>();

            char[] wdl_maof = { 'F', 'O', 'A', 'M' }; // WDL chunk containing offsets in the file to each MARE

            s = s.Trim('"');
            
            try
            {
                using (BinaryReader wdlReader = new BinaryReader(File.OpenRead(s ?? throw new InvalidOperationException())))
                {
                    string newName = Path.GetFileNameWithoutExtension(s) + "_occ.wdt";
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter occWriter = new BinaryWriter(ms))
                    {
                        // Header
                        char[] mverHeader = { 'R', 'E', 'V', 'M' };
                        const uint mverS = 4;
                        const uint mverVersion = 18;
                        occWriter.Write(mverHeader);
                        occWriter.Write(mverS);
                        occWriter.Write(mverVersion);

                        char[] maoiHeader = { 'I', 'O', 'A', 'M' };
                        occWriter.Write(maoiHeader);

                        var maoiSizePos = occWriter.BaseStream.Position;
                        occWriter.Write(mverS); // Doesn't matter what we write here, we'll go back to it later with the right size.

                        // We just add the static size to this later
                        uint offsetVal = 0;
                        uint sizeOfMAOH = 1090;
                        while (wdlReader.BaseStream.Position <= wdlReader.BaseStream.Length)
                        {
                            // Check magic
                            char[] c = wdlReader.ReadChars(4);
                            int size = Convert.ToInt32(wdlReader.ReadUInt32());

                            if (c.SequenceEqual(wdl_maof))
                            {
                                Console.WriteLine("Found MAOF...");
                                uint maoiSizeSoFar = 0;
                                // Read each maof entry, if offset isn't 0, write data in occwriter, and fetch data from binaryreader
                                for (ushort y = 0; y < 64; y++)
                                {
                                    for (ushort x = 0; x < 64; x++)
                                    {
                                        Console.WriteLine("Found XY offset: " + x + "," + y);
                                        uint maofOff = wdlReader.ReadUInt32();
                                        if (maofOff != 0)
                                        {
                                            occWriter.Write(x);
                                            occWriter.Write(y);
                                            occWriter.Write(offsetVal);
                                            occWriter.Write(sizeOfMAOH);
                                            offsetVal += sizeOfMAOH;

                                            maoiSizeSoFar += 12;

                                            // We come back to this after reading contents it references
                                            var curReadPos = wdlReader.BaseStream.Position;
                                            wdlReader.BaseStream.Position = maofOff;

                                            // Ignore header
                                            wdlReader.ReadBytes(8);

                                            // Dump all contents for later
                                            short[] contentsOfHeight = new short[545];
                                            for (int i = 0; i < 545; i++)
                                            {
                                                // We lower the wdl
                                                short finalVal = wdlReader.ReadInt16();

                                                // Check to avoid underflow
                                                if ((short.MinValue + 50) > finalVal)
                                                    finalVal = short.MinValue;
                                                else
                                                    finalVal -= ValueToLowerBy;

                                                contentsOfHeight[i] = finalVal;
                                            }

                                            MapHeightData.Add(contentsOfHeight);

                                            wdlReader.BaseStream.Position = curReadPos;


                                        }
                                    }
                                }
                                Console.WriteLine("Done with MAOF...");


                                // Go back and write the actual size of this.
                                var curWritePos = occWriter.BaseStream.Position;
                                occWriter.BaseStream.Position = maoiSizePos;
                                occWriter.Write(maoiSizeSoFar);
                                occWriter.BaseStream.Position = curWritePos;

                                // Write mapheightdata in MAOH

                                char[] maohHeader = { 'H', 'O', 'A', 'M' };
                                occWriter.Write(maohHeader);

                                uint maohSize = 1090 * Convert.ToUInt32(MapHeightData.Count);
                                occWriter.Write(maohSize); 

                                Console.WriteLine("MapHeightData writing, count: " + MapHeightData.Count);

                                for (int i = 0; i < MapHeightData.Count; i++)
                                {
                                    Console.Write(".");
                                    for(int j = 0; j < 545; j++)
                                        occWriter.Write(MapHeightData[i][j]);
                                }

                                break;
                            }
                            else
                            {
                                wdlReader.ReadBytes(size);
                            }
                        }

                        // Transfer to file

                        Console.WriteLine("Writing all to file...");
                        ms.Position = 0;
                        using (FileStream destFile = File.OpenWrite(newName))
                        {
                            ms.CopyTo(destFile);
                            ms.Flush();
                            destFile.Flush();
                        }

                        Console.WriteLine("Done!");
                        
                        Console.Beep();
                        Console.ReadLine();

                        Environment.Exit(0);
                    }
                }
            }
            catch (Exception e)
            {
                Abort(e.Message);
            }
        }
    }
}
