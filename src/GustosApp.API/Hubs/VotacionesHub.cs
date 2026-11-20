using Microsoft.AspNetCore.SignalR;

using GustosApp.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GustosApp.API.Hubs
{
    [Authorize]
    public class VotacionesHub : Hub
    {
        private readonly IGrupoRepository _grupoRepository;

        public VotacionesHub(IGrupoRepository grupoRepository)
        {
            _grupoRepository = grupoRepository;
        }

        public override async Task OnConnectedAsync()
        {
            var grupoIdTexto = Context.GetHttpContext()?.Request.Query["grupoId"].ToString();
            var firebaseUid = Context.User?.FindFirst("user_id")?.Value
                ?? Context.User?.FindFirst("sub")?.Value;

            if (!Guid.TryParse(grupoIdTexto, out var grupoId) || string.IsNullOrWhiteSpace(firebaseUid))
            {
                Context.Abort();
                return;
            }

            var esMiembro = await _grupoRepository.UsuarioEsMiembroAsync(
                grupoId,
                firebaseUid,
                Context.ConnectionAborted);

            if (!esMiembro)
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, grupoId.ToString());

            await base.OnConnectedAsync();
        }
    }
}
