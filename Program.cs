/*
 *  MixOptimize - C&C Renegade map and mod package optimizer
 *  Copyright (C) 2023 Unstoppable
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace mixoptimize;

public class MixOptimize
{
    const string Version = "1.0";

    static void PrintHelp()
    {
        Console.WriteLine("Usage: mixoptimize [options] <input file>");
        Console.WriteLine("Description:");
        Console.WriteLine("  This tool attempts to decrease the file size and increase the performance");
        Console.WriteLine("  by applying various optimizations to .mix and .pkg files, without any guarantee.");
        Console.WriteLine("Options:");
        Console.WriteLine("  --skip-texture-conversion: Skips texture conversion to DDS.");
        Console.WriteLine("  --skip-texture-resize: Skips resizing textures to a square.");
        Console.WriteLine("  --max-exponent <value>: The maximum power of two to use while resizing. (Default: 9 -> 2^9 = 512)");
        Console.WriteLine("  --skip-sounds: Skips re-encoding sounds to MP3 @ 128 kbps.");
        Console.WriteLine("  --skip-confirmation: Skips confirmation for the changes to be done.");
        Console.WriteLine("  --read-stdin: Reads the file from standard input instead. (Implies --skip-confirmation)");
        Console.WriteLine("  --out: Output file. (Required if --read-stdin is specified)");
        Console.WriteLine();
        Console.WriteLine("MixOptimize is licensed under GNU General Public License v3.0. Please view LICENSE file for details.");
        Console.WriteLine();
        Console.WriteLine("MixOptimize uses the following open-source libraries:");
        Console.WriteLine("  Magick.NET by Dirk Lemstra");
        Console.WriteLine("  NAudio by Mark Heath & NAudio Contributors");
        Console.WriteLine("  NAudio.Lame by Corey Murtagh");
    }

    static void PrintSplash()
    {
        Console.WriteLine($"MixOptimize utility {Version} - by Unstoppable");
    }

    public static bool SkipTextureConversion = false;
    public static bool SkipTextureResize = false;
    public static bool SkipSounds = false;
    public static bool ReadStandardInput = false;
    public static bool SkipConfirmation = false;
    public static int MaxExponent = 9;
    public static string OutputFile = null;

    static void Main(string[] args)
    {
        PrintSplash();

        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        ConsoleManager.Init();
        MixOptimizeMain(args);
        ConsoleManager.Shutdown();
    }

    static void MixOptimizeMain(string[] args)
    {
        string targetFile = string.Empty;

        bool readingOptions = true;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--skip-texture-conversion")
            {
                SkipTextureConversion = true;
            }
            else if (args[i] == "--skip-texture-resize")
            {
                SkipTextureResize = true;
            }
            else if (args[i] == "--max-exponent")
            {
                if (!int.TryParse(args[++i], out MaxExponent))
                {
                    ConsoleManager.Print("Invalid value for switch \"--max-exponent\" specified. Using default value... (Must be numeric)");
                    MaxExponent = 9;
                }
            }
            else if (args[i] == "--skip-sounds")
            {
                SkipSounds = true;
            }
            else if (args[i] == "--read-stdin")
            {
                ReadStandardInput = true;
                SkipConfirmation = true;
            }
            else if (args[i] == "--skip-confirmation")
            {
                SkipConfirmation = true;
            }
            else if (args[i] == "--out")
            {
                OutputFile = args[++i];
                if (!Uri.IsWellFormedUriString(OutputFile, UriKind.RelativeOrAbsolute))
                {
                    ConsoleManager.Print("Invalid output file specified.");
                    OutputFile = null;
                }
            }
            else
            {
                readingOptions = false;
            }

            if (!readingOptions)
            {
                targetFile = string.Join(" ", args.Skip(i));
                break;
            }
        }


        byte[] mixFileBytes = null;

        if (ReadStandardInput)
        {
            if (OutputFile == null)
            {
                ConsoleManager.Print("--out switch has to be specified when --read-stdin is used!");
                return;
            }

            targetFile = "STDIN";

            using (var stdin = Console.OpenStandardInput())
            {
                ConsoleManager.SetLoading("Reading the file from standard input...");
                
                var result = InputTask.ReadFromStream(stdin);
                result.Wait();
                mixFileBytes = result.Result;

                ConsoleManager.ResetLoading();
            }
        }
        else
        {
            if (File.Exists(targetFile))
            {
                OutputFile = targetFile;
                mixFileBytes = File.ReadAllBytes(targetFile);
            }
            else
            {
                ConsoleManager.Print($"Could not find the specified file \"{targetFile}\".");
                return;
            }
        }

        if (mixFileBytes.Length == 0)
        {
            ConsoleManager.Print("The specified Mix file is empty.");
            return;
        }

        MixPackageClass mixFile;
        try
        {
            mixFile = MixPackageClass.Load(mixFileBytes);
        }
        catch (Exception ex)
        {
            ConsoleManager.Print($"Failed to load Mix file: {ex.Message}");
            return;
        }

        ConsoleManager.InitProgress(0, mixFile.FileCount);
        ConsoleManager.SetLoading($"Analyzing the Mix file...");

        Dictionary<int, IAnalysisResult> results = new();
        while (ConsoleManager.ProgressBarValue != ConsoleManager.ProgressBarMaximum)
        {
            ConsoleManager.SetLoading($"Analyzing the Mix file... ({ConsoleManager.ProgressBarValue}/{ConsoleManager.ProgressBarMaximum})");

            var file = mixFile.Files[ConsoleManager.ProgressBarValue];
            var name = file.FileName;
            var ext = Path.GetExtension(name)[1..].ToUpper();

            switch (ext)
            {
                case "DDS":
                   results.Add(ConsoleManager.ProgressBarValue, ImageAnalyzer.AnalyzeDDS(file.Data));
                   break;

                case "TGA":
                    results.Add(ConsoleManager.ProgressBarValue, ImageAnalyzer.AnalyzeTGA(file.Data));
                    break;

                case "MP3":
                    results.Add(ConsoleManager.ProgressBarValue, AudioAnalyzer.AnalyzeMP3(file.Data));
                    break;

                case "WAV":
                    results.Add(ConsoleManager.ProgressBarValue, AudioAnalyzer.AnalyzeWAV(file.Data));
                    break;
            }

            ConsoleManager.ProgressBarValue++;
        }

        ConsoleManager.ResetProgress();

        var actionCount = results.Count(x => x.Value.NeedsAction);
        if (actionCount == 0)
        {
            ConsoleManager.Print($"Mix file {Path.GetFileName(targetFile)} does not require any optimizations.");
            return;
        }
        else
        {
            ConsoleManager.Print($"Mix file {Path.GetFileName(targetFile)} has {actionCount} optimizations available.");

            foreach (var entry in results)
            {
                if (!entry.Value.NeedsAction) continue;

                var file = mixFile.Files[entry.Key];
                var name = file.FileName;
                var ext = Path.GetExtension(name)[1..].ToUpper();

                ConsoleManager.Print($"File: {name}");

                if (entry.Value is ImageAnalysisResult imageResult)
                {
                    if (imageResult.NeedsConversion)
                    {
                        ConsoleManager.Print($"   ► Format: {ext} --> DDS");
                    }

                    if (imageResult.NeedsResizing)
                    {
                        ConsoleManager.Print($"   ► Size: {imageResult.OldSize.Width}x{imageResult.OldSize.Height} --> {imageResult.NewSize.Width}x{imageResult.NewSize.Height}");
                    }
                }
                else if (entry.Value is AudioAnalysisResult audioResult)
                {
                    if (audioResult.NeedsConversion)
                    {
                        ConsoleManager.Print($"   ► Format: {ext} --> MP3");
                    }

                    if (audioResult.NeedsBitrateProcessing)
                    {
                        ConsoleManager.Print($"   ► Bit Rate: {audioResult.OldBitrate / 1000} kbps --> {audioResult.NewBitrate / 1000} kbps");
                    }
                }
            }
        }

        ConsoleManager.ResetLoading();

        if (!SkipConfirmation)
        {
            bool validInput = false;
            do
            {
                ConsoleManager.Print($"Would you like to apply all {actionCount} optimizations? (Y/N)");

                var key = Console.ReadKey(true);
                if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                {
                    validInput = true;
                }
                else if (key.KeyChar == 'n' || key.KeyChar == 'N')
                {
                    ConsoleManager.Print("Aborting...");
                    return;
                }
            } while (!validInput);
        }

        // Create a backup of the original file.
        if (!ReadStandardInput)
        {
            ConsoleManager.SetLoading($"Creating backup of {Path.GetFileName(targetFile)}...");
            var backupFileName = Path.GetFileName(targetFile) + "-BACKUP";
            var backupPath = Path.Combine(Path.GetDirectoryName(targetFile), backupFileName);
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Copy(targetFile, backupPath);
            ConsoleManager.ResetLoading();
            ConsoleManager.Print($"Backup has been saved as {backupFileName}.");
        }


        // Start applying optimizations.
        ConsoleManager.InitProgress(0, actionCount);
        ConsoleManager.SetLoading("Applying optimizations...");
        foreach (var entry in results.Where(x => x.Value.NeedsAction))
        {
            var file = mixFile.Files[entry.Key];
            var name = file.FileName;
            var ext = Path.GetExtension(name)[1..].ToUpper();

            ConsoleManager.SetLoading($"Applying optimizations for {name}... ({ConsoleManager.ProgressBarValue}/{ConsoleManager.ProgressBarMaximum})");

            try
            {
                switch (ext)
                {
                    case "DDS":
                        file.Data = ImageAnalyzer.ApplyDDS(file.Data, (ImageAnalysisResult)entry.Value);
                        break;

                    case "TGA":
                        if (((ImageAnalysisResult)entry.Value).NeedsConversion)
                        {
                            file.FileName = Path.GetFileNameWithoutExtension(name) + ".dds";
                        }
                        file.Data = ImageAnalyzer.ApplyTGA(file.Data, (ImageAnalysisResult)entry.Value);
                        break;

                    case "MP3":
                        file.Data = AudioAnalyzer.ApplyMP3(file.Data, (AudioAnalysisResult)entry.Value);
                        break;

                    case "WAV":
                        var oldName = file.FileName;
                        file.FileName = Path.GetFileNameWithoutExtension(name) + ".mp3";
                        file.Data = AudioAnalyzer.ApplyWAV(file.Data, (AudioAnalysisResult)entry.Value);
                        LevelDataManipulator.ReplaceLevelData(mixFile, oldName, file.FileName);
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleManager.Print($"Failed to apply optimizations for {name}: {ex.Message}");
                actionCount--;
            }

            ConsoleManager.ProgressBarValue++;
        }

        ConsoleManager.ResetProgress();
        ConsoleManager.SetLoading("Saving Mix file...");

        mixFile.Save(OutputFile);
        ConsoleManager.ResetLoading();

        ConsoleManager.Print($"Applied {actionCount} optimizations to {Path.GetFileName(OutputFile)}.");
    }
}