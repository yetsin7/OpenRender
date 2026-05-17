using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace OpenRender.Rendering;

/// <summary>
/// Core Vulkan Context management.
/// Handles Instance, Physical Device, and Logical Device.
/// </summary>
public unsafe class VulkanContext : IDisposable
{
    private Vk _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private uint _graphicsQueueFamilyIndex;

    public Vk Vk => _vk;
    public Instance Instance => _instance;
    public PhysicalDevice PhysicalDevice => _physicalDevice;
    public Device Device => _device;
    public Queue GraphicsQueue => _graphicsQueue;
    public uint GraphicsQueueFamilyIndex => _graphicsQueueFamilyIndex;

    public VulkanContext()
    {
        _vk = Vk.GetApi();
        CreateInstance();
        PickPhysicalDevice();
        CreateLogicalDevice();
    }

    private void CreateInstance()
    {
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("OpenRender"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("OpenRender Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        var extensions = new List<string> { KhrSurface.ExtensionName, KhrWin32Surface.ExtensionName };
        var layers = new List<string>();

#if DEBUG
        const string validationLayer = "VK_LAYER_KHRONOS_validation";
        if (IsInstanceLayerAvailable(validationLayer))
        {
            layers.Add(validationLayer);
        }
#endif

        var extensionNames = SilkMarshal.StringArrayToPtr(extensions);
        var layerNames = SilkMarshal.StringArrayToPtr(layers);

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensions.Count,
            PpEnabledExtensionNames = (byte**)extensionNames,
            EnabledLayerCount = (uint)layers.Count,
            PpEnabledLayerNames = (byte**)layerNames
        };

        var result = _vk.CreateInstance(&createInfo, null, out _instance);
        if (result != Result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to create Vulkan instance. Result: {result}. " +
                $"Requested extensions: {string.Join(", ", extensions)}. " +
                $"Requested layers: {(layers.Count > 0 ? string.Join(", ", layers) : "none")}.");
        }

        SilkMarshal.Free((IntPtr)appInfo.PApplicationName);
        SilkMarshal.Free((IntPtr)appInfo.PEngineName);
        SilkMarshal.Free((IntPtr)extensionNames);
        SilkMarshal.Free((IntPtr)layerNames);
    }

    private bool IsInstanceLayerAvailable(string layerName)
    {
        uint layerCount = 0;
        _vk.EnumerateInstanceLayerProperties(ref layerCount, null);
        if (layerCount == 0)
        {
            return false;
        }

        var availableLayers = stackalloc LayerProperties[(int)layerCount];
        _vk.EnumerateInstanceLayerProperties(ref layerCount, availableLayers);

        for (int index = 0; index < layerCount; index++)
        {
            var availableName = SilkMarshal.PtrToString((nint)availableLayers[index].LayerName);
            if (string.Equals(availableName, layerName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);

        if (deviceCount == 0)
        {
            throw new Exception("Failed to find GPUs with Vulkan support.");
        }

        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devices);

        // Pick the first discrete GPU or first available
        for (int i = 0; i < deviceCount; i++)
        {
            PhysicalDeviceProperties properties;
            _vk.GetPhysicalDeviceProperties(devices[i], &properties);
            
            if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
            {
                _physicalDevice = devices[i];
                break;
            }
        }

        if (_physicalDevice.Handle == 0)
        {
            _physicalDevice = devices[0];
        }
    }

    private void CreateLogicalDevice()
    {
        FindQueueFamilies();

        float queuePriority = 1.0f;
        var queueCreateInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        var deviceFeatures = new PhysicalDeviceFeatures();
        
        var extensions = new List<string> { KhrSwapchain.ExtensionName };
        var extensionNames = SilkMarshal.StringArrayToPtr(extensions);

        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)extensions.Count,
            PpEnabledExtensionNames = (byte**)extensionNames
        };

        if (_vk.CreateDevice(_physicalDevice, &createInfo, null, out _device) != Result.Success)
        {
            throw new Exception("Failed to create Vulkan logical device.");
        }

        _vk.GetDeviceQueue(_device, _graphicsQueueFamilyIndex, 0, out _graphicsQueue);
        
        SilkMarshal.Free((IntPtr)extensionNames);
    }

    private void FindQueueFamilies()
    {
        uint queueFamilyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, null);

        var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, ref queueFamilyCount, queueFamilies);

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                _graphicsQueueFamilyIndex = i;
                return;
            }
        }

        throw new Exception("Failed to find a graphics queue family.");
    }

    public void Dispose()
    {
        _vk.DestroyDevice(_device, null);
        _vk.DestroyInstance(_instance, null);
        _vk.Dispose();
    }
}
