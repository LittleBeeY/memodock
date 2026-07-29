using System.Windows.Media;
using MemoDock.Core.Models;

namespace MemoDock.Services;

public sealed record ForegroundAppSnapshot(AppDescriptor Descriptor, ImageSource? Icon);
