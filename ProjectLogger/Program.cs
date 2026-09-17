using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

const string PipeName = "FlasherCore.Logger";
Console.Title = "FlasherCore Log Server";
Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=================================================");
Console.WriteLine("   FlasherCore Log Server   ");
Console.WriteLine($"   Pipe: {PipeName}");
Console.WriteLine("=================================================\n");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        var pipeSecurity = new PipeSecurity();
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(everyone, PipeAccessRights.FullControl, AccessControlType.Allow));

        await using var server = NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0, 0,
            pipeSecurity
        );

        await server.WaitForConnectionAsync(cts.Token);
        Console.WriteLine($"[Conn] Client Connected!");

        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
        char[] buffer = new char[4096];

        while (!cts.Token.IsCancellationRequested && server.IsConnected)
        {
            int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;

            string content = new string(buffer, 0, bytesRead);
            PrintLog(content);
        }
    }
    catch (OperationCanceledException) { break; }
    catch (IOException) { Console.WriteLine("[Disc] Client Disconnected."); }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Err] {ex.Message}");
        Console.ResetColor();
        Thread.Sleep(1000);
    }
}

void PrintLog(string msg)
{
    if (msg.Contains("[ERR]")) Console.ForegroundColor = ConsoleColor.Red;
    else if (msg.Contains("[WRN]")) Console.ForegroundColor = ConsoleColor.Yellow;
    else if (msg.Contains("[INF]")) Console.ForegroundColor = ConsoleColor.Green;
    else if (msg.Contains("[DBG]")) Console.ForegroundColor = ConsoleColor.Gray;
    else Console.ForegroundColor = ConsoleColor.White;

    Console.WriteLine(msg);
    Console.ResetColor();
}