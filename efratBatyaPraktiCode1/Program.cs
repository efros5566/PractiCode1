using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        var languageOption = new Option<string>("--language","List of programming languages to include in the bundle");
        languageOption.AddAlias("-l");
        var outputOption = new Option<FileInfo>("--output","File path and name for the bundled file");
        outputOption.AddAlias("-o");
        var noteOption = new Option<bool>("--note","Add source code information as a comment in the bundled file");
        noteOption.AddAlias("-n");
        var sortOption = new Option<string>("--sort","Sort order for bundled files (name or type)");
        sortOption.AddAlias("-s");
        sortOption.SetDefaultValue("name");
        var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "Remove empty lines from source code");
        removeEmptyLinesOption.AddAlias("-rel");
        var authorOption = new Option<string>("--author", "Author of the bundled file");
        authorOption.AddAlias("-a");
        #region bundle
        var bundleCommand = new Command("bundle", "Bundle code files to a single file");
        bundleCommand.AddOption(languageOption);
        bundleCommand.AddOption(outputOption);
        bundleCommand.AddOption(noteOption);
        bundleCommand.AddOption(sortOption);
        bundleCommand.AddOption(removeEmptyLinesOption);
        bundleCommand.AddOption(authorOption);
        bundleCommand.SetHandler((language, output, note, sort, removeEmptyLine, author) =>
        {
            try
            {
                // Validate and process language option
                string[] selectedLanguages = ValidateAndProcessLanguageOption(language);

                // Validate and process output option
                string outputPath = ValidateAndProcessOutputOption(output);

                // Get all code files in the current directory
                DirectoryInfo currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
                FileInfo[] codeFiles = currentDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly);

                // Filter code files based on selected languages
                IEnumerable<FileInfo> selectedCodeFiles;
                if (selectedLanguages.Contains("all", StringComparer.OrdinalIgnoreCase))
                {
                    selectedCodeFiles = codeFiles;
                }
                else
                {
                    selectedCodeFiles = codeFiles.Where(file =>
                        selectedLanguages.Contains(Path.GetExtension(file.FullName).TrimStart('.'), StringComparer.OrdinalIgnoreCase));
                }

                // Sort code files based on the specified order (name or type)
                if (sort.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    selectedCodeFiles = selectedCodeFiles.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase);
                }
                else if (sort.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    selectedCodeFiles = selectedCodeFiles.OrderBy(file => Path.GetExtension(file.FullName), StringComparer.OrdinalIgnoreCase);
                }
                // Remove empty lines if specified
                if (removeEmptyLine)
                {
                    RemoveEmptyLinesFromCodeFiles(selectedCodeFiles);
                }

                // Create bundled file
                using (var bundledFileStream = File.Create(outputPath))
                {
                    // Add source code information as a comment if specified
                    if (note || !string.IsNullOrWhiteSpace(author))
                    {
                        AddSourceCodeInfo(bundledFileStream, selectedCodeFiles, author, note);
                    }
                }

                Console.WriteLine($"Bundle created successfully at: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }, languageOption, outputOption, noteOption, sortOption, removeEmptyLinesOption, authorOption);
        #endregion
        #region rsp
        var rspCommand = new Command("create-rsp", "Create a response file");
        rspCommand.AddOption(outputOption);


        rspCommand.SetHandler((output) =>
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(output.FullName))
                {
                    var bundleOutput = PromptForValue<string>("route/name bundle file");
                    var lang = PromptForValue<string>("Language");
                    var note = PromptForValue<bool>("Include note (true/false)");
                    var sort = PromptForValue<string>("Sort by (name/type)");
                    var removeEmptyLines = PromptForValue<bool>("Remove empty lines (true/false)");
                    var author = PromptForValue<string>("Author:");

                    sw.WriteLine($"-o {bundleOutput}");
                    sw.WriteLine($"-l {lang}");
                    sw.WriteLine($"-n {note}");
                    sw.WriteLine($"-s {sort}");
                    sw.WriteLine($"-rel {removeEmptyLines}");
                    sw.WriteLine($"-a {author}");
                }

                Console.WriteLine($"Response file created at {output.FullName}");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }, outputOption);
        #endregion
        var rootCommand = new RootCommand("Root command for file bundler CLI");
        rootCommand.AddCommand(bundleCommand);
        rootCommand.AddCommand(rspCommand);
        rootCommand.InvokeAsync(args).Wait();

    }

    private static string[] ValidateAndProcessLanguageOption(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language option is required.");
        }

        return language.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(lang => lang.Trim()).ToArray();
    }

    private static string ValidateAndProcessOutputOption(FileInfo output)
    {
        if (string.IsNullOrWhiteSpace(output.FullName))
        {
            throw new ArgumentException("Output option is required.");
        }

        return Path.GetFullPath(output.FullName);
    }

    private static void AddSourceCodeInfo(FileStream bundledFileStream, IEnumerable<FileInfo> selectedCodeFiles, string author, bool note)
    {
        using (StreamWriter writer = new StreamWriter(bundledFileStream))
        {

            if (!string.IsNullOrWhiteSpace(author))
            {
                writer.WriteLine($"// Author: {author}");
            }
            writer.WriteLine();
            writer.WriteLine($"// - {bundledFileStream.Name}");
            foreach (var codeFile in selectedCodeFiles)
            {
                writer.WriteLine();
                writer.Flush();
                bundledFileStream.Seek(0, SeekOrigin.End);
                // Add note comments if specified
                if (note)
                {
                    writer.WriteLine($"//     - {codeFile.Name}");
                    // writer.WriteLine($"//     - {codeFile.FullName}");
                }
                writer.Flush();
                bundledFileStream.Seek(0, SeekOrigin.End);

                // Copy content of each code file to the bundled file
                using (var codeFileStream = codeFile.OpenRead())
                {
                    codeFileStream.CopyTo(bundledFileStream);
                }
                // Flush and close the writer to allow writing to bundledFileStream again
                writer.Flush();
                bundledFileStream.Seek(0, SeekOrigin.End);
            }
        }
    }

    private static void RemoveEmptyLinesFromCodeFiles(IEnumerable<FileInfo> codeFiles)
    {
        foreach (var codeFile in codeFiles)
        {
            // Read the content of the code file
            string content = File.ReadAllText(codeFile.FullName);

            // Remove empty lines
            content = string.Join("\r\n", content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));

            // Write the modified content back to the file
            File.WriteAllText(codeFile.FullName, content);
        }
    }
    static T PromptForValue<T>(string promptMessage)
    {
        Console.Write($"{promptMessage}: ");
        var input = Console.ReadLine();

        try
        {
            return (T)Convert.ChangeType(input, typeof(T));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return default;
        }
    }

}




