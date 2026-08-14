using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Tests;

namespace ActivityPub.Tests.LoadTesting;

public class LoadTestProgram
{
    public static async Task Main(string[] args)
    {
        var factory = new TestWebApplicationFactory();
        
        var demo = new LoadTestDemo(factory);
        await demo.RunDemoAsync();

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
