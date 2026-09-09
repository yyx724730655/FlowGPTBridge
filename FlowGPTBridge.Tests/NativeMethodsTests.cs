using System.Reflection;
using System.Runtime.InteropServices;
using FlowGPTBridge.Services;

namespace FlowGPTBridge.Tests;

public sealed class NativeMethodsTests
{
    [Fact]
    public void GetCurrentThreadId_IsImportedFromKernel32()
    {
        var nativeMethods = typeof(ShortcutSender).Assembly.GetType(
            "FlowGPTBridge.Services.NativeMethods",
            throwOnError: true)!;
        var method = nativeMethods.GetMethod(
            "GetCurrentThreadId",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var import = method.GetCustomAttribute<DllImportAttribute>();

        Assert.NotNull(import);
        Assert.Equal("kernel32.dll", import.Value, ignoreCase: true);
    }
}
