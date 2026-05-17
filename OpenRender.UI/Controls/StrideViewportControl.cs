using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using OpenRender.Engine;

namespace OpenRender.Controls;

/// <summary>
/// Avalonia control that hosts the Stride Engine viewport using NativeControlHost.
/// </summary>
public class StrideViewportControl : NativeControlHost
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    private const uint WS_CHILD = 0x40000000;
    private const uint WS_VISIBLE = 0x10000000;

    private IntPtr _hWnd;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Create a simple child window as a container for Stride
            _hWnd = CreateWindowEx(
                0,
                "STATIC", // Use a standard system class for the container
                "StrideViewport",
                WS_CHILD | WS_VISIBLE,
                0, 0, (int)Bounds.Width, (int)Bounds.Height,
                parent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_hWnd != IntPtr.Zero)
            {
                // Initialize Stride Engine with this handle
                InitializeStride(_hWnd);
                return new PlatformHandle(_hWnd, "HWND");
            }
        }

        return base.CreateNativeControlCore(parent);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_hWnd != IntPtr.Zero)
        {
            DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        base.DestroyNativeControlCore(control);
    }

    private void InitializeStride(IntPtr handle)
    {
        // We initialize the engine in a background thread or let the service handle it
        StrideEngineService.Instance.Initialize(handle, (int)Bounds.Width, (int)Bounds.Height);
    }
}
