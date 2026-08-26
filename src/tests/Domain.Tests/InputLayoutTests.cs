using System.Reflection;
using System.Runtime.InteropServices;
using Capture;
using Xunit;

namespace Domain.Tests;

public class InputLayoutTests
{
    [Fact]
    public void input_только_type_и_mi_без_ручного_padding()
    {
        var fields = typeof(NativeMethods.Input)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Equal(2, fields.Length);
        Assert.Contains(fields, field => field.Name == nameof(NativeMethods.Input.type));
        Assert.Contains(fields, field => field.Name == nameof(NativeMethods.Input.mi));
    }

    [Fact]
    public void размер_input_совпадает_с_win32()
    {
        var expected = IntPtr.Size == 4 ? 28 : 40;
        Assert.Equal(expected, Marshal.SizeOf<NativeMethods.Input>());
    }

    [Fact]
    public void mi_сразу_после_выравнивания_type()
    {
        var expectedOffset = IntPtr.Size == 4 ? 4 : 8;
        Assert.Equal(expectedOffset, Marshal.OffsetOf<NativeMethods.Input>(nameof(NativeMethods.Input.mi)).ToInt32());
    }
}
