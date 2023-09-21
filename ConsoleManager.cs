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

public static class ConsoleManager
{
    private static int pBarMin, pBarMax, pBarVal;
    private static int pSpinnerStep;
    private static CancellationTokenSource cancelToken;

    public static string LoadingText;
    public static bool DoLoader;
    public static bool DoProgressBar;

    public static int ProgressBarMinimum
    {
        get => pBarMin;
        set
        {
            pBarMin = value;
            ProgressChanged();
        }
    }

    public static int ProgressBarMaximum
    {
        get => pBarMax;
        set
        {
            pBarMax = value;
            ProgressChanged();
        }
    }

    public static int ProgressBarValue
    {
        get => pBarVal;
        set
        {
            pBarVal = value;
            if (pBarVal < pBarMin)
                pBarVal = pBarMin;
            if (pBarVal > pBarMax)
                pBarVal = pBarMax;

            ProgressChanged();
        }
    }


    private static object PrintLock = new();

    public static void Init()
    {
        LoadingText = string.Empty;
        DoLoader = false;
        DoProgressBar = false;
        pBarMin = 0;
        pBarMax = 0;
        pBarVal = 0;

        cancelToken = new();
        Task.Run(AnimationTask);
    }

    public static void Shutdown()
    {
        ResetLoading();
        ResetProgress();
        cancelToken.Cancel();
    }

    public static void SetLoading(string text)
    {
        DoLoader = true;
        LoadingText = text;
        LoaderTick();
    }

    public static void ResetLoading()
    {
        if (!DoLoader) return;

        DoLoader = false;
        LoadingText = string.Empty;

        if (!DoLoader && !DoProgressBar)
        {
            Console.Write("\r" + new string(' ', Console.WindowWidth));
            Console.Write("\r");
        }
    }

    public static void InitProgress(int min, int max)
    {
        DoProgressBar = true;
        pBarMin = min;
        pBarMax = max;
        pBarVal = min;
        ProgressChanged();
    }

    public static void ResetProgress()
    {
        if (!DoProgressBar) return;

        DoProgressBar = false;
        pBarMin = 0;
        pBarMax = 0;
        pBarVal = 0;

        if (!DoLoader)
        {
            var top = Console.CursorTop;
            Console.CursorLeft = 0;
            Console.Write("\r" + new string(' ', Console.WindowWidth));
            Console.CursorTop = top;
            Console.CursorLeft = 0;
        }
        else
        {
            LoaderTick();
        }
    }

    public static void Print(string text)
    {
        lock (PrintLock)
        {
            if (DoLoader || DoProgressBar)
            {
                var top = Console.CursorTop;
                Console.CursorLeft = 0;
                Console.Write("\r" + new string(' ', Console.WindowWidth));
                Console.CursorTop = top;
                Console.CursorLeft = 0;
            }

            Console.WriteLine(text);
            LoaderTick();
        }
    }

    private static void ProgressChanged()
    {
        if (DoProgressBar)
        {
            lock (PrintLock)
            {
                int top = Console.CursorTop;

                int len = LoadingText.Length + 3;
                int requiredLen = Console.WindowWidth - len;
                if (requiredLen > 3)
                {
                    double progress = ((pBarVal - pBarMin) * 100.0 / (pBarMax - pBarMin));

                    if (requiredLen > 12)
                    {
                        int blocks = requiredLen - 8;
                        int drawBlocks = (int)(progress * blocks / 100.0);
                        Console.Write($" [{new string('#', drawBlocks)}{new string(' ', blocks - drawBlocks)}] {(int)progress}%");
                    }
                    else
                    {
                        Console.Write($" {(int)progress}%");
                    }
                }

                Console.CursorTop = top;
                Console.CursorLeft = len - 1;
            }
        }
    }

    private static void LoaderTick(bool update = false)
    {
        if (DoLoader)
        {
            lock (PrintLock)
            {
                int top = Console.CursorTop;

                Console.Write("\r" + new string(' ', Console.WindowWidth));
                Console.CursorTop = top;

                switch (update ? pSpinnerStep++ : pSpinnerStep)
                {
                    case 0:
                        Console.Write("\r- ");
                        break;
                    case 1:
                        Console.Write("\r\\ ");
                        break;
                    case 2:
                        Console.Write("\r| ");
                        break;
                    case 3:
                        Console.Write("\r/ ");
                        if (update)
                            pSpinnerStep = 0;
                        break;
                }

                Console.Write(LoadingText);

                Console.CursorTop = top;
                Console.CursorLeft = LoadingText.Length + 2;
            }

            if (DoProgressBar)
            {
                ProgressChanged();
            }
        }
    }

    private static void AnimationTask()
    {
        while (!cancelToken.IsCancellationRequested)
        {
            LoaderTick(true);
            Thread.Sleep(125);
        }
    }
}