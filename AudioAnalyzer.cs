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

public struct AudioAnalysisResult : IAnalysisResult
{
    public bool NeedsAction => NeedsConversion || NeedsBitrateProcessing;
    public bool NeedsConversion = false;
    public bool NeedsBitrateProcessing = false;
    public int OldBitrate = 0;
    public int NewBitrate = 0;

    public AudioAnalysisResult()
    {

    }
}

public class AudioAnalyzer
{
    public static AudioAnalysisResult AnalyzeMP3(byte[] mp3Bytes)
    {
        var result = new AudioAnalysisResult();

        using (var ms = new MemoryStream(mp3Bytes))
        using (Mp3FileReader reader = new Mp3FileReader(ms))
        {
            result.OldBitrate = reader.Mp3WaveFormat.AverageBytesPerSecond * 8;

            if (result.OldBitrate - 128000 > 3000)
            {
                result.NeedsBitrateProcessing = true;
                result.NewBitrate = 128000;
                return result;
            }
            else
            {
                Mp3Frame frame = reader.ReadNextFrame();
                while (frame != null)
                {
                    if (frame.BitRate - 128000 > 3000)
                    {
                        result.NeedsBitrateProcessing = true;
                        result.NewBitrate = 128000;
                        return result;
                    }

                    frame = reader.ReadNextFrame();
                }
            }
        }

        return result;
    }

    public static AudioAnalysisResult AnalyzeWAV(byte[] wavBytes)
    {
        var result = new AudioAnalysisResult()
        {
            NeedsConversion = true,
            NewBitrate = 128000
        };

        using (var ms = new MemoryStream(wavBytes))
        {
            WaveFileReader reader = new WaveFileReader(ms);
            result.OldBitrate = reader.WaveFormat.AverageBytesPerSecond * 8;

            if (result.OldBitrate - 128000 > 3000)
            {
                result.NeedsBitrateProcessing = true;
                return result;
            }
        }

        return result;
    }

    public static byte[] ApplyMP3(byte[] mp3Bytes, AudioAnalysisResult analysis)
    {
        if (analysis.NeedsBitrateProcessing)
        {
            using (var retMs = new MemoryStream())
            using (var ms = new MemoryStream(mp3Bytes))
            using (Mp3FileReader reader = new Mp3FileReader(ms))
            using (var writer = new LameMP3FileWriter(retMs, reader.WaveFormat, 128))
            {
                reader.CopyTo(writer);
                writer.Flush();
                return retMs.ToArray();
            }
        }
        else
        {
            return mp3Bytes;
        }
    }

    public static byte[] ApplyWAV(byte[] wavBytes, AudioAnalysisResult analysis)
    {
        using (var retMs = new MemoryStream())
        using (var ms = new MemoryStream(wavBytes))
        {
            if (analysis is { NeedsConversion: true, NeedsBitrateProcessing: true })
            {
                using (WaveFileReader reader = new WaveFileReader(ms))
                using (var writer = new LameMP3FileWriter(retMs, reader.WaveFormat, 128))
                {
                    reader.CopyTo(writer);
                    writer.Flush();
                    return retMs.ToArray();
                }
            }
            else
            {
                return wavBytes;
            }
        }
    }
}
