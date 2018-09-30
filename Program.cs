using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SessionSlicer
{
    class Program
    {
        /// <summary>
        /// Session Slicer main entry point
        /// </summary>
        /// <param name="args">gCodeFileName SliceHeight1 [SliceHeight2 ...]</param>
        static void Main(string[] args)
        {
            // If no arguments are provided display hexp syntax and exit
            if (args.Count() == 0)
            {
                Console.WriteLine("\r\nSyntax:");
                Console.WriteLine("SessionSlicer gCodeFile");
                Console.WriteLine("SessionSlicer gCodeFile sessionEndHeight1 [sessionEndHeight2 ...]");
                Console.WriteLine("");
                Environment.Exit(128);
            }

            // Read gCodeFile
            string[] contents = System.IO.File.ReadAllLines(args[0]);

            // Get Object Height And Last Draw Line
            string actualHeight = "";
            int actualDelta = 0;
            GetFileInfo(args[0], ref contents, ref actualHeight, ref actualDelta);

            // If no slice height infromation is provided, exit with syntax help but include object height information
            if (args.Count()==1)
            {
                Console.WriteLine("\r\nSyntax:");
                Console.WriteLine("SessionSlicer gCodeFile");
                Console.WriteLine("SessionSlicer gCodeFile sessionEndHeight1 [sessionEndHeight2 ...]");
                Console.WriteLine("");
                Console.WriteLine("Print '" + args[0] + "' has an maximum height of " + actualHeight);
                Console.WriteLine("");
                Environment.Exit(129);
            }

            // Determine the root filename for used for making the session files 
            string gCodeFile = (System.IO.Path.GetDirectoryName(args[0])=="") ? Environment.CurrentDirectory+"/"+ System.IO.Path.GetFileNameWithoutExtension(args[0]) : System.IO.Path.GetDirectoryName(args[0])+"/"+ System.IO.Path.GetFileNameWithoutExtension(args[0]);
            
            // Remove the gFile argument so we are left withheight slice arguments only
            args = args.Skip(1).ToArray();
            List<string> slices = args.ToList();

            // Add a pseudo slice argument for the last session. Since print will not exceed this value height, all remaining lines will be placed in the last session 
            slices.Add("9999.0");

            // Read configuration from the configuration file
            int startCodeLines = int.Parse(System.Configuration.ConfigurationManager.AppSettings["startCodeLines"]);
            string startCode = System.Configuration.ConfigurationManager.AppSettings["startCode"];
            int endCodeLines = int.Parse(System.Configuration.ConfigurationManager.AppSettings["endCodeLines"]);
            string endCode = System.Configuration.ConfigurationManager.AppSettings["endCode"];
            int backupMode = int.Parse(System.Configuration.ConfigurationManager.AppSettings["backupMode"]);

            // Update startCodeLines and endCodeLines if set to automatic (-1)
            CalculateAutomaticStartAndEndLines(ref startCodeLines, ref endCodeLines, contents, actualDelta);

            // Prepare session files. This replaces the content if the session files already exist
            PrepareSessionFiles(slices.Count, gCodeFile);

            // Perform session slicing
            ProcessSlicing(contents, slices, gCodeFile, startCodeLines, endCodeLines, startCode, endCode, backupMode);
        }

        /// <summary>
        /// Method for getting object height and last draw line from the gCode.
        /// 
        /// Algorithm:
        /// 
        /// The code looks for the last Z transition which is followed by some printing (G0 or G1 codes) at that height.
        /// By doing this, any Z transition used to move the extruder away from the finished print are ignored and the
        /// actual height of the printed object is obtained.
        /// 
        /// </summary>
        /// <param name="fileName">gCode file name</param>
        /// <param name="contents">Contents of the gCode</param>
        /// <param name="actualHeight">Actual object height</param>
        /// <param name="actualDelta">Last draw gCode</param>
        public static void GetFileInfo(string fileName, ref string[] contents, ref string actualHeight, ref int actualDelta)
        {
            // Read contents
            contents = System.IO.File.ReadAllLines(fileName);

            string height = "";
            int lastDelta = 0;
            int deltas = 0;
            // Cycle through all content lines
            for (int i = 0; i < contents.Count(); i++)
            {
                // Look for G0 or G1 codes which include a Z transition
                if (Microsoft.VisualBasic.CompilerServices.Operators.LikeString(contents.ElementAt(i), "G0*X*Y*Z*", Microsoft.VisualBasic.CompareMethod.Text) || Microsoft.VisualBasic.CompilerServices.Operators.LikeString(contents.ElementAt(i), "G1*X*Y*Z*", Microsoft.VisualBasic.CompareMethod.Text))
                {
                    // If the previous height had gCodes for drawing at that level, store it as the height of the object
                    if (deltas > 2) { actualHeight = height; actualDelta = lastDelta + 1; }

                    // Update height
                    height = contents.ElementAt(i).Substring(contents.ElementAt(i).IndexOf("Z") + 1);
                    height = (height + " ").Substring(0, (height + " ").IndexOf(" "));

                    // Reset the number of draws at this height
                    deltas = 0;
                }
                else if (Microsoft.VisualBasic.CompilerServices.Operators.LikeString(contents.ElementAt(i), "G0*X*Y*", Microsoft.VisualBasic.CompareMethod.Text) || Microsoft.VisualBasic.CompilerServices.Operators.LikeString(contents.ElementAt(i), "G1*X*Y*", Microsoft.VisualBasic.CompareMethod.Text))
                {
                    // Remember the last line which printed at the current height
                    lastDelta = i;
                    // Count the number of lines that printed at this level
                    deltas++;
                }
            }
        }

        /// <summary>
        /// Method used to determine the number of start lines and end lines that should be repeated at the start and end of each session.
        /// The values are only changed if they are -1 in the configuration (indicating automatic detection).
        /// </summary>
        /// <param name="startCodeLines">Number of lines of code to be repeated at the beginning of each session</param>
        /// <param name="endCodeLines">Number of lines of code to be repeated at the end of each session</param>
        /// <param name="contents">gCode contents</param>
        /// <param name="actualDelta">Previus determined last draw line</param>
        public static void CalculateAutomaticStartAndEndLines(ref int startCodeLines, ref int endCodeLines, string[] contents, int actualDelta)
        {
            // Start lines are repeated until the first G0 or G1 code that includes X, Y and Z values. 
            if (startCodeLines < 0)
            {
                startCodeLines = 0;
                while (!Microsoft.VisualBasic.CompilerServices.Operators.LikeString(contents.ElementAt(startCodeLines), "G0*X*Y*Z*", Microsoft.VisualBasic.CompareMethod.Text) && !Microsoft.VisualBasic.CompilerServices.Operators.LikeString(contents.ElementAt(startCodeLines), "G1*X*Y*Z*", Microsoft.VisualBasic.CompareMethod.Text)) { startCodeLines++; }
            }
            // All lines beyond the previously calculated Last Draw Line are repeated
            if (endCodeLines < 0)
            {
                endCodeLines = contents.Count() - actualDelta;
            }
        }

        /// <summary>
        /// Method to prepare session files. Overwrites any existing content which ensures that if the application is run multile times the lastest run replacess the previous runs. 
        /// </summary>
        /// <param name="slices">Number of sessions</param>
        /// <param name="gCodeFile">Root name of the gCode file for determining session file names</param>
        public static void PrepareSessionFiles(int slices, string gCodeFile)
        {
            for (int i = 1; i <= slices; i++)
            {
                System.IO.File.WriteAllText(gCodeFile + ".Session" + i.ToString("d2") + ".gcode", "; ************\r\n;  Session " + i.ToString("d2") + "\r\n; ***********\r\n;\r\n");
            }
        }

        /// <summary>
        /// Method to actually perform the session slicing
        /// 
        /// Algorithm:
        /// 
        /// 1. Copy out all lines until the first M109. This prevents height check from tripping if initiailization raises Z
        /// 2. Check G0 or G1 commands for a Z component. If the height exceeds the slice height:
        ///      a. Write the end code to the current session
        ///      b. Increment the session
        ///      c. Write the start code to the session
        ///      d. Write the backfill if necessary
        ///      e. Move on to the next height slice 
        /// 
        /// </summary>
        /// <param name="contents">gCode contents</param>
        /// <param name="slices">Provided height slices</param>
        /// <param name="gCodeFile">Root file name for the session files</param>
        /// <param name="startCodeLines">Number of start lines to be repeated at the start of each session file</param>
        /// <param name="endCodeLines">Number of end lines to be placed at the end of each session file</param>
        /// <param name="startCode">Additional manually configured code to be placed at the start of each additional session file</param>
        /// <param name="endCode">Additional manullay configured code to be placed at the endof each session file except the last</param>
        /// <param name="backupMode">Backfill mode (0=off, 1=Comments Only, 2=Comments And G? F Commands)</param>
        public static void ProcessSlicing(string[] contents, List<string> slices, string gCodeFile, int startCodeLines, int endCodeLines, string startCode, string endCode, int backupMode)
        {
            // Initial values for the session (i.e. session 1) and print (i.e. starting height at 0.0) 
            int session = 1;
            double currentHeight = 0.0;

            // Create a flag for identifying when we should start Z checks
            bool sawM109 = false;

            // Create backfill queue with empty entries
            Queue<string> lookback = new Queue<string>();
            int lookbackLength = 5;
            for (int i = 0; i < lookbackLength; i++) { lookback.Enqueue(" "); }

            // Process each entry in the original gCode file
            string height = "";
            foreach (string content in contents)
            {
                // Toggle presence of M109 if line starts with M109
                if (content.StartsWith("M109 ")) { sawM109 = true; }

                // Process Z checks only if M109 has already been encountered 
                if (sawM109)
                {
                    // Look of G0 or G1 commands with Z offset
                    if (Microsoft.VisualBasic.CompilerServices.Operators.LikeString(content, "G0*Z*", Microsoft.VisualBasic.CompareMethod.Text) || Microsoft.VisualBasic.CompilerServices.Operators.LikeString(content, "G1*Z*", Microsoft.VisualBasic.CompareMethod.Text))
                    {
                        // Determine the height
                        height = content.Substring(content.IndexOf("Z") + 1);
                        height = (height + " ").Substring(0, (height + " ").IndexOf(" "));
                        currentHeight = double.Parse(height);

                        // Compare the new current height against the next slice height 
                        if (currentHeight > double.Parse(slices[0]))
                        {
                            // Current height exceeds next slice height, transition to next session

                            // Write out end code lines
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; ---END LINES--------------------------------------------------------------------\r\n");
                            for (int i = contents.Count() - endCodeLines; i < contents.Count(); i++)
                            {
                                System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", contents.ElementAt(i) + "\r\n");
                            }
                            // Write out manually configured end code
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; ---END CODE---------------------------------------------------------------------\r\n");
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", endCode.Replace("|", "\r\n").Replace("{S}", session.ToString("d2")).Replace("{H}", currentHeight.ToString()).Replace("{H+}", (currentHeight + 5).ToString()) + "\r\n");
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; --------------------------------------------------------------------------------\r\n");
                            // Advance the session pointer
                            session++;
                            // Remove the processed slice height from the remaining slice heights
                            slices.RemoveAt(0);
                            // Write out the start code lines
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; ---START LINES------------------------------------------------------------------\r\n");
                            for (int i = 0; i < startCodeLines; i++)
                            {
                                System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", contents.ElementAt(i) + "\r\n");
                            }
                            // Write out manually configured start code
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; ---START CODE-------------------------------------------------------------------\r\n");
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", startCode.Replace("|", "\r\n").Replace("{S}", session.ToString("d2")).Replace("{H}", currentHeight.ToString()).Replace("{H+}", (currentHeight + 5).ToString()) + "\r\n");
                            // Write out backfill if enabled
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; ---BACK FILL--------------------------------------------------------------------\r\n");
                            for (int i = 0; i < lookbackLength; i++)
                            {
                                string item = lookback.Dequeue();
                                lookback.Enqueue(" ");
                                // Check if comments backfill is active
                                if (((backupMode & 1) > 0) && (item.StartsWith(";")))
                                {
                                    System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", item + "\r\n");
                                }
                                // Check if "G? F*" command backfill is active
                                else if (((backupMode & 2) > 0) && (Microsoft.VisualBasic.CompilerServices.Operators.LikeString(item, "G0*F*", Microsoft.VisualBasic.CompareMethod.Text) || Microsoft.VisualBasic.CompilerServices.Operators.LikeString(item, "G1*F*", Microsoft.VisualBasic.CompareMethod.Text)))
                                {
                                    System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", item + "\r\n");
                                }
                            }
                            System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", "; --------------------------------------------------------------------------------\r\n");
                        }
                        // Update console output 
                        Console.Clear();
                        Console.WriteLine("Session " + session.ToString("d2") + "...");
                        Console.WriteLine("   Height = " + currentHeight + " (Next Session @ " + slices[0] + ")");
                    }
                }
                // Drops oldest entry in the queue
                lookback.Dequeue();
                // Add current entry into the queue
                lookback.Enqueue(content);
                // Write out the entry to the session file
                System.IO.File.AppendAllText(gCodeFile + ".Session" + session.ToString("d2") + ".gcode", content + "\r\n");
            }
        }
    }
}
