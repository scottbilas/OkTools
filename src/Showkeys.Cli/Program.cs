﻿using System.Collections.Concurrent;
using static Vezel.Cathode.Terminal;

OutLine("Ctrl-C to quit");
OutLine();

EnableRawMode();

try
{
    var queue = new BlockingCollection<byte>();

    var task = Task.Run(() =>
    {
        var array = new byte[1];
        for (;;)
        {
            Read(array);
            queue.Add(array[0]);
        }

        // ReSharper disable once FunctionNeverReturns
    });

    for (var next = 0; next != 3; )
    {
        Out((next = queue.Take()) switch
        {
            3 => "^C",
            0x1b => "^[",
            '\r' => "\n",
            _ => ((char)next).ToString()
        });
    }

    await task;
}
finally
{
    DisableRawMode();
}
