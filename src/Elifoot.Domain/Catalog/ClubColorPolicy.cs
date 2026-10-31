using Elifoot.Contracts;
using System.Text.RegularExpressions;
namespace Elifoot.Domain;

public static class ClubColorPolicy
{
    public static bool Hex(string? value) => value!=null && Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$");
    public static void Validate(ClubColorPalette value)
    {
        if(!Hex(value.PrimaryColor)||(value.SecondaryColor!=null&&!Hex(value.SecondaryColor))||
            (value.PreferredTextColor!=null&&!Hex(value.PreferredTextColor))||value.ColorConfidence is <0 or >1||
            string.IsNullOrWhiteSpace(value.ColorSource)||string.IsNullOrWhiteSpace(value.Evidence)||
            string.IsNullOrWhiteSpace(value.Version)||!Enum.IsDefined(value.Gender))throw new ArgumentException("Paleta RGB, fonte ou identidade inválida.");
    }
    public static ClubColorPalette Personalized(string primary,string secondary,string country,FootballGender gender,Guid id)=>
        new(primary.ToUpperInvariant(),secondary.ToUpperInvariant(),null,"Montagem privada do usuário",false,DateTime.UtcNow,1,
            "custom014c-v1",country,gender,"custom014c",id.ToString("N"),"Cores definidas explicitamente no editor pré-save",true,"UserDefined");
    public static ClubColorPalette? ForAssembly(ClubExportDto club,ClubExportDto? baseline,bool trusted,FootballGender gender,string? countryCode=null)
    {
        if(!Hex(club.PrimaryColorHex)||!Hex(club.SecondaryColorHex))return null;
        if(trusted && baseline?.ColorPalette==null && baseline?.PrimaryColorHex==club.PrimaryColorHex && baseline?.SecondaryColorHex==club.SecondaryColorHex)return null;
        if(trusted && baseline?.ColorPalette is {} original && club.PrimaryColorHex.Equals(original.PrimaryColor,StringComparison.OrdinalIgnoreCase)
            && club.SecondaryColorHex.Equals(original.SecondaryColor??original.PrimaryColor,StringComparison.OrdinalIgnoreCase)
            && baseline.Name==club.Name && baseline.Country==club.Country && original.Gender==gender)return original;
        return Personalized(club.PrimaryColorHex,club.SecondaryColorHex,countryCode??club.Country,gender,club.Id);
    }
    public static void Validate(AppearancePreferencesDto p)
    {
        if(p.Mode is not("Club" or "Classic" or "Custom" or "Neutral" or "ClubGlobal"))throw new ArgumentException("Modo de aparência inválido.");
        if(p.ThemeVariant is not(null or "Classico" or "Escuro" or "AltoContraste"))throw new ArgumentException("Variação de tema inválida.");
        if(p.Brightness is <-2 or >2)throw new ArgumentException("Brilho deve estar entre -2 e 2.");
        if(p.MatchColors is not(null or "Strong" or "Neutral"))throw new ArgumentException("Modo de cores das partidas inválido.");
        foreach(var c in new[]{p.Background,p.Primary,p.Secondary,p.Text,p.Accent})if(c!=null&&!Hex(c))throw new ArgumentException("Use hexadecimal RGB #RRGGBB.");
        if(p.Mode=="Custom" && (p.Background==null||p.Primary==null||p.Secondary==null||p.Text==null||p.Accent==null))throw new ArgumentException("Informe todas as cores da personalização.");
    }
}
