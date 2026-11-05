using Nurtura.Core.Services;

namespace Nurtura.Web.Areas.Professional.Models;

// The professional's chat page: the room, and the pre-consultation summary for the Summary tab
// (null once the session has ended).
public sealed record ChatPageViewModel(ChatRoomInfo Room, ConsultationSummary? Summary);
