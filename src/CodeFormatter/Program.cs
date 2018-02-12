// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Buildalyzer;
using Buildalyzer.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.DotNet.CodeFormatting;

namespace CodeFormatter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            //Environment.SetEnvironmentVariable("VisualStudioVersion", "15.0");

            //Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH",
            //    @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\msbuild.exe");

            //Environment.SetEnvironmentVariable("VSINSTALLDIR",
            //    @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise");

            //Environment.SetEnvironmentVariable("MSBuildSDKsPath", "C:\\Program Files\\dotnet\\sdk\\2.1.4\\Sdks");

            //Environment.SetEnvironmentVariable("MSBuildExtensionsPath32", @"C:\Users\Brad\Code\codeformatter\src\CodeFormatter\bin\Debug\net461");

            var result = CommandLineParser.Parse(args);
            if (result.IsError)
            {
                Console.Error.WriteLine(result.Error);
                CommandLineParser.PrintUsage();
                return -1;
            }

            var options = result.Options;
            int exitCode;
            switch (options.Operation)
            {
                case Operation.ListRules:
                    RunListRules();
                    exitCode = 0;
                    break;
                case Operation.Format:
                    exitCode = RunFormat(options);
                    break;
                default:
                    throw new Exception("Invalid enum value: " + options.Operation);
            }

            return 0;
        }

        private static void RunListRules()
        {
            var rules = FormattingEngine.GetFormattingRules();
            Console.WriteLine("{0,-20} {1}", "Name", "Description");
            Console.WriteLine("==============================================");
            foreach (var rule in rules)
            {
                Console.WriteLine("{0,-20} :{1}", rule.Name, rule.Description);
            }
        }

        private static int RunFormat(CommandLineOptions options)
        {
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Console.CancelKeyPress += delegate { cts.Cancel(); };

            try
            {
                RunFormatAsync(options, ct).Wait(ct);
                Console.WriteLine("Completed formatting.");
                return 0;
            }
            catch (AggregateException ex)
            {
                var typeLoadException = ex.InnerExceptions.FirstOrDefault() as ReflectionTypeLoadException;
                if (typeLoadException == null)
                    throw;

                Console.WriteLine("ERROR: Type loading error detected. In order to run this tool you need either Visual Studio 2015 or Microsoft Build Tools 2015 tools installed.");
                var messages = typeLoadException.LoaderExceptions.Select(e => e.Message).Distinct();
                foreach (var message in messages)
                    Console.WriteLine("- {0}", message);

                return 1;
            }
        }

        private static async Task<int> RunFormatAsync(CommandLineOptions options, CancellationToken cancellationToken)
        {
            var engine = FormattingEngine.Create();
            engine.PreprocessorConfigurations = options.PreprocessorConfigurations;
            engine.FileNames = options.FileNames;
            engine.CopyrightHeader = options.CopyrightHeader;
            engine.AllowTables = options.AllowTables;
            engine.Verbose = options.Verbose;

            if (!SetRuleMap(engine, options.RuleMap))
            {
                return 1;
            }

            foreach (var item in options.FormatTargets)
            {
                await RunFormatItemAsync(engine, item, options.Language, cancellationToken);
            }

            return 0;
        }

        private static async Task RunFormatItemAsync(IFormattingEngine engine, string item, string language, CancellationToken cancellationToken)
        { 
            Console.WriteLine(Path.GetFileName(item));
            string extension = Path.GetExtension(item);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".rsp"))
            {
                using (var workspace = ResponseFileWorkspace.Create())
                {
                    Project project = workspace.OpenCommandLineProject(item, language);
                    await engine.FormatProjectAsync(project, cancellationToken);
                }
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".sln"))
            {
                AnalyzerManager manager = new AnalyzerManager(item);
                

                using (var workspace = manager.GetWorkspace())
                {
                    
                    //workspace.LoadMetadataForReferencedProjects = true;
                    //var solution = await workspace.OpenSolutionAsync(item, cancellationToken);
                    await engine.FormatSolutionAsync(workspace.CurrentSolution, cancellationToken);
                }
            }
            else
            {
                using (var workspace = MSBuildWorkspace.Create())
                {
                    workspace.LoadMetadataForReferencedProjects = true;
                    var project = await workspace.OpenProjectAsync(item, cancellationToken);
                    await engine.FormatProjectAsync(project, cancellationToken);
                }
            }
        }

        private static bool SetRuleMap(IFormattingEngine engine, ImmutableDictionary<string, bool> ruleMap)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            foreach (var entry in ruleMap)
            {
                var rule = engine.AllRules.Where(x => comparer.Equals(x.Name, entry.Key)).FirstOrDefault();
                if (rule == null)
                {
                    Console.WriteLine("Could not find rule with name {0}", entry.Key);
                    return false;
                }

                engine.ToggleRuleEnabled(rule, entry.Value);
            }

            return true;
        }
    }
}
