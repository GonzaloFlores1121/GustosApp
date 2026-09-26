namespace GustosApp.Application.Common.Exceptions;

public sealed class PreferenciasGrupoDesdePerfilException : Exception
{
    public PreferenciasGrupoDesdePerfilException()
        : base("Los gustos del grupo se toman del perfil actual de cada participante. Editá tus gustos desde tu perfil.")
    {
    }
}
