using TorchSharp;

namespace ZHI.Core;

/// <summary>
/// 自动检测 CUDA，若可用则使用 GPU 加速
/// </summary>
public static class Device
{
    public static torch.Device TorchDevice { get; private set; } = torch.CPU;
    public static string Name { get; private set; } = "CPU";

    public static void Initialize()
    {
        try
        {
            if (torch.cuda.is_available())
            {
                TorchDevice = torch.CUDA;
                var count = torch.cuda.device_count();
                Name = $"CUDA ({count} device(s))";
                Console.WriteLine($"[Device] GPU 已启用 ({count} device(s))");
            }
            else
            {
                Console.WriteLine("[Device] CUDA 不可用，使用 CPU");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Device] CUDA 检测失败: {ex.Message}，使用 CPU");
        }
    }
}
