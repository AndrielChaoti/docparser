using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBDocParse
{
    class Program
    {
        private static Regex sLinePattern = new Regex(@"^#{4}\s`(?:.+?)`\s(?<function>.+?)\((?<argString>.+)*\)", RegexOptions.Multiline);
        private static Regex sArgsPattern = new Regex(@"(?:`.+?`)\s(?<argName>.+?\w+(?:\s\.{3})?)");
        private static Object _writeLock = new Object();

        private static List<SublimeCompletions.APICompletion> GetAPICompletions(String text)
        {
            List<SublimeCompletions.APICompletion> completions = new List<SublimeCompletions.APICompletion>();

            // Parse a line match 
            MatchCollection lineMatches = sLinePattern.Matches(text);

            Parallel.ForEach(lineMatches.OfType<Match>(), lineMatch =>
            {
                String functionName = lineMatch.Groups["function"].Value; ;
                String rawArgString = lineMatch.Groups["argString"].Value; ;

                // A null function name shouldn't exist, at all...
                if (functionName == null) return;
                StringBuilder argumentSignature = new StringBuilder();

                // Parse an argString match
                MatchCollection argMatches = sArgsPattern.Matches(rawArgString);
                String[] argNames = new String[argMatches.Count];
                int index = 0;
                foreach (Match argMatch in argMatches)
                {
                    argNames[index] = argMatch.Groups["argName"].Value;
                    index++;
                }

                //Build Argument Signature
                if (argNames.Count() != 0)
                {
                    argumentSignature.Append("${1:");
                    for (int i = 0; i < argNames.Count(); i++)
                    {
                        argumentSignature.Append("${" + (i + 2) + ":");
                        argumentSignature.Append(argNames[i]);
                        argumentSignature.Append("}");
                        if (i != argNames.Count() - 1)
                        {
                            argumentSignature.Append(", ");
                        }
                    }
                    argumentSignature.Append("}");
                }

                Trace.WriteLine(String.Format("{0,-30} {1}", functionName, argumentSignature));

                // Add to our "collection"
                lock (_writeLock)
                {
                    completions.Add(new SublimeCompletions.APICompletion { trigger = functionName, contents = functionName + "(" + argumentSignature + ")" });
                }
            });
            return completions;
        }

        private static string GetFile(String path)
        {
            try
            {
                string sout = "";
                using (StreamReader sr = new StreamReader(path))
                {
                    String cf = sr.ReadToEnd();
                    sout += cf;
                }
                Trace.WriteLine("Loaded file " + path);
                return sout;
            }
            catch (FileNotFoundException fnfe)
            {
                Trace.WriteLine("File " + fnfe.FileName + " not found.");
                throw;
            }
            catch (Exception e)
            {
                Trace.WriteLine("An error has occured while loading files...");
                throw;
            }
        }

        static void Main(string[] args)
        {
            //Trace.Listeners.Add(new TextWriterTraceListener(@"%userprofile%\Desktop\debug.log"));
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;

            Trace.WriteLine("Starbound Documentation Parsing");
            Trace.WriteLine("Copyright (©)2017 Donald Granger");

            if (args.Length < 2)
            {
                // no file argument, stop running
                Trace.WriteLine("Invalid number of arguments.");
                Trace.WriteLine("Usage: DocParse.exe <input file> [<input file> ...] <output file>");
                Environment.Exit(1);
            }

            try
            {

                // Read files, cache in list
                Trace.Write("Loading all files... \n");
                List<String> files = args.Take(args.Length - 1).AsParallel().Select(GetFile).ToList();
                Trace.WriteLine("\tComplete!");

                // create our object...
                SublimeCompletions completionsFile = new SublimeCompletions
                {
                    source = "source.lua",
                    completions = new List<SublimeCompletions.APICompletion>()
                };
                // Initialize some things:
                Trace.WriteLine("Parsing File Data...");
                Trace.WriteLine(String.Format("{0,-30} {1}", "Name", "Parameters"));
                // Run through our files, and the lines in them
                Parallel.ForEach(files, file => GetAPICompletions(file).AsParallel().ForAll(elem =>
                {
                    lock (completionsFile) completionsFile.completions.Add(elem);
                }));

                Trace.WriteLine("Found " + completionsFile.completions.Count + " total element(s) in " + files.Count + " file(s).");
                Trace.WriteLine("Writing output to " + args.Last() + "...");
                // Serialize the output
                using (StreamWriter sw = new StreamWriter(args.Last()))
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    jw.Formatting = Formatting.Indented;
                    jw.WriteStartObject();
                    jw.WritePropertyName("source");
                    jw.WriteValue(completionsFile.source);
                    jw.WritePropertyName("completions");
                    jw.WriteStartArray();
                    for (int i = 0; i < completionsFile.completions.Count; i++)
                    {
                        jw.WriteStartObject();
                        jw.Formatting = Formatting.None;
                        jw.WritePropertyName("trigger");
                        jw.WriteValue(completionsFile.completions[i].trigger);
                        if (completionsFile.completions[i].contents != null)
                        {
                            jw.WritePropertyName("contents");
                            jw.WriteValue(completionsFile.completions[i].contents);
                        }
                        jw.WriteEndObject();
                        jw.Formatting = Formatting.Indented;

                    }
                    jw.Formatting = Formatting.Indented;
                    jw.WriteEndArray();
                    jw.WriteEndObject();
                }

            }
            catch (Exception e)
            {
                Trace.WriteLine("Exception Details: " + e.GetType() + " " + e.Message);
                Trace.WriteLine("Stack Trace:\n" + e.StackTrace);
                Environment.Exit(255);
            }
        }
    }
}
