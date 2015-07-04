using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NCCHInfo
{
    class Program
    {
        static void Main(string[] args)
        {
            //Check that at least one argument has been passed to the application
            if(args.Length == 0)
            {
                //If not, show usage and quit
                PrintUsage();
                Environment.Exit(1);
            }

            Console.WriteLine();

            //Find all targetted files
            List<string> targetfiles = new List<string>();
            foreach(string filepattern in args)
            {
                targetfiles.AddRange(FilePatternResolver.resolve(filepattern));
            }

            //Process the files one by one
            List<NCCHInfoEntry> entries = new List<NCCHInfoEntry>();
            foreach(string file in targetfiles)
            {
                Parser parser = new Parser();
                List<NCCHInfoEntry> newentries = parser.parse(file);
                entries.AddRange(newentries);

                Console.WriteLine();

            }

            //Generate the output file
            FileStream outputfile = File.Open("ncchinfo.bin", FileMode.Create);
            BinaryWriter writer = new BinaryWriter(outputfile, Encoding.ASCII);
            NCCHInfoHeader header = new NCCHInfoHeader((UInt32) entries.Count);
            writer.Write(StructMarshaling.StructToBytes<NCCHInfoHeader>(header));
            foreach(NCCHInfoEntry entry in entries)
            {
                writer.Write(StructMarshaling.StructToBytes<NCCHInfoEntry>(entry));
            }
            writer.Close();

            //Signal completion
            Console.WriteLine("Done!");
        }




        public static void PrintUsage()
        {
            Console.WriteLine("No 3DS ROM file provided. Please provide a file.");
        }
    }
}