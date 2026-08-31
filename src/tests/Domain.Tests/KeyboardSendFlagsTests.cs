using Domain.RemoteDesktop;
using Xunit;

namespace Domain.Tests;

public class KeyboardSendFlagsTests
{
    /// <summary>
    /// Обычная буква — без extended, down без KEYUP.
    /// </summary>
    [Fact]
    public void From_буква_вниз_без_флагов()
    {
        Assert.Equal(0u, KeyboardSendFlags.From(down: true, vk: 0x41));
    }

    /// <summary>
    /// Отпускание буквы — только KEYUP.
    /// </summary>
    [Fact]
    public void From_буква_вверх_keyup()
    {
        Assert.Equal(KeyboardSendFlags.KeyUp, KeyboardSendFlags.From(down: false, vk: 0x41));
    }

    /// <summary>
    /// Стрелки, Delete, правый Ctrl — extended, иначе Windows шлёт не ту клавишу.
    /// </summary>
    [Theory]
    [InlineData(0x25)]
    [InlineData(0x26)]
    [InlineData(0x27)]
    [InlineData(0x28)]
    [InlineData(0x2E)]
    [InlineData(0xA3)]
    [InlineData(0xA5)]
    public void From_extended_клавиши(int vk)
    {
        Assert.Equal(KeyboardSendFlags.Extended, KeyboardSendFlags.From(down: true, vk: vk));
        Assert.Equal(
            KeyboardSendFlags.Extended | KeyboardSendFlags.KeyUp,
            KeyboardSendFlags.From(down: false, vk: vk));
    }

    /// <summary>
    /// Vk 0 не отправляем — это неизвестный code с сервера.
    /// </summary>
    [Fact]
    public void ShouldSend_false_если_vk_0()
    {
        Assert.False(KeyboardSendFlags.ShouldSend(0));
        Assert.True(KeyboardSendFlags.ShouldSend(0x41));
    }

    /// <summary>
    /// Перед диспетчером и при стопе сессии отпускаем оба Ctrl, Alt и Shift.
    /// </summary>
    [Fact]
    public void ModifierVirtualKeys_левые_и_правые_ctrl_alt_shift()
    {
        Assert.Equal([0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5], KeyboardSendFlags.ModifierVirtualKeys);
    }
}
