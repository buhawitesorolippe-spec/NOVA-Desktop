using NAudio.CoreAudioApi;

namespace Nova.Desktop;

internal sealed record AudioDevice(string Id, string Name);

internal static class AudioDeviceCatalog
{
    public static IReadOnlyList<AudioDevice> GetCaptureDevices() => GetDevices(DataFlow.Capture);

    public static IReadOnlyList<AudioDevice> GetPlaybackDevices() => GetDevices(DataFlow.Render);

    private static IReadOnlyList<AudioDevice> GetDevices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<AudioDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device)
                devices.Add(new AudioDevice(device.ID, device.FriendlyName));
        }
        return devices;
    }
}
