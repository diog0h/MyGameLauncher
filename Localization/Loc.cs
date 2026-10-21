using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace GameLauncher.Localization;

/// <summary>
/// Textos da app em várias línguas. No XAML usa-se {l:Tr Chave}; no código Loc.T("Chave") ou Loc.F("Chave", args).
/// Ao mudar de língua, todos os textos ligados por {l:Tr} atualizam-se sozinhos.
/// Para acrescentar uma língua: juntar uma entrada em <see cref="Languages"/> e uma coluna em cada linha da tabela.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public sealed record Language(string Code, string Name, string Culture);

    public static readonly Language[] Languages =
    {
        new("pt", "Português", "pt-PT"),
        new("en", "English", "en-US"),
        new("es", "Español", "es-ES"),
        new("fr", "Français", "fr-FR"),
        new("de", "Deutsch", "de-DE"),
    };

    private static readonly Dictionary<string, string[]> Strings = new();
    private static int _index;

    public static Loc Instance { get; }
    public static string Code => Languages[_index].Code;
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("pt-PT");

    /// <summary>A língua mudou (para atualizar textos criados no código).</summary>
    public static event Action? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => T(key);

    public static string T(string key) =>
        Strings.TryGetValue(key, out var row) ? row[_index] : key;

    public static string F(string key, params object[] args) => string.Format(Culture, T(key), args);

    public static void SetLanguage(string? code)
    {
        int i = Array.FindIndex(Languages, l => l.Code == code);
        if (i < 0) i = Array.FindIndex(Languages, l => l.Code == DetectSystemLanguage());
        _index = i >= 0 ? i : Array.FindIndex(Languages, l => l.Code == "en"); // inglês por defeito
        Culture = CultureInfo.GetCultureInfo(Languages[_index].Culture);
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
        Changed?.Invoke();
    }

    /// <summary>Língua do Windows, se for uma das suportadas; senão inglês (a língua predefinida).</summary>
    public static string DetectSystemLanguage()
    {
        var sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Any(l => l.Code == sys) ? sys : "en";
    }

    private static void Add(string key, string pt, string en, string es, string fr, string de) =>
        Strings[key] = new[] { pt, en, es, fr, de };

    static Loc()
    {
        // ---------------- barra lateral e janela
        Add("Nav_Library", "Biblioteca", "Library", "Biblioteca", "Bibliothèque", "Bibliothek");
        Add("Nav_Home", "Início", "Home", "Inicio", "Accueil", "Start");
        Add("Nav_AllGames", "Todos os jogos", "All games", "Todos los juegos", "Tous les jeux", "Alle Spiele");
        Add("Nav_Favorites", "Favoritos", "Favorites", "Favoritos", "Favoris", "Favoriten");
        Add("Nav_Collections", "Coleções", "Collections", "Colecciones", "Collections", "Sammlungen");
        Add("Nav_NewCollection", "Nova coleção", "New collection", "Nueva colección", "Nouvelle collection", "Neue Sammlung");
        Add("Nav_NewCollectionHint", "Nome e Enter", "Name, then Enter", "Nombre y Enter", "Nom puis Entrée", "Name und Enter");
        Add("Nav_Activity", "Atividade", "Activity", "Actividad", "Activité", "Aktivität");
        Add("Nav_Playtime", "Tempo de jogo", "Playtime", "Tiempo de juego", "Temps de jeu", "Spielzeit");
        Add("Nav_Settings", "Definições", "Settings", "Ajustes", "Paramètres", "Einstellungen");
        Add("Fullscreen", "Ecrã inteiro", "Full screen", "Pantalla completa", "Plein écran", "Vollbild");
        Add("ExitFullscreen", "Sair do ecrã inteiro", "Exit full screen", "Salir de pantalla completa", "Quitter le plein écran", "Vollbild beenden");
        Add("BackTooltip", "Voltar (Esc)", "Back (Esc)", "Atrás (Esc)", "Retour (Échap)", "Zurück (Esc)");
        Add("SearchHint", "Pesquisar jogos", "Search games", "Buscar juegos", "Rechercher des jeux", "Spiele suchen");
        Add("Loading", "A procurar jogos…", "Looking for games…", "Buscando juegos…", "Recherche des jeux…", "Spiele werden gesucht…");

        Add("Nav_Stores", "Lojas", "Stores", "Tiendas", "Boutiques", "Stores");
        Add("Nav_AddGame", "Adicionar jogo", "Add game", "Añadir juego", "Ajouter un jeu", "Spiel hinzufügen");
        Add("Cancel", "Cancelar", "Cancel", "Cancelar", "Annuler", "Abbrechen");

        // ---------------- adicionar jogo à mão
        Add("Add_Title", "Adicionar jogo", "Add game", "Añadir juego", "Ajouter un jeu", "Spiel hinzufügen");
        Add("Add_Desc",
            "Qualquer jogo ou launcher: escolhe o .exe, um atalho ou um link. A loja que escreveres aparece nos filtros da barra lateral.",
            "Any game or launcher: pick the .exe, a shortcut or a link. The store you type shows up in the sidebar filters.",
            "Cualquier juego o launcher: elige el .exe, un acceso directo o un enlace. La tienda que escribas aparece en los filtros de la barra lateral.",
            "N’importe quel jeu ou launcher : choisis le .exe, un raccourci ou un lien. La boutique saisie apparaît dans les filtres de la barre latérale.",
            "Jedes Spiel oder jeder Launcher: Wähle die .exe, eine Verknüpfung oder einen Link. Der eingegebene Store erscheint in den Filtern der Seitenleiste.");
        Add("Add_File", "Ficheiro", "File", "Archivo", "Fichier", "Datei");
        Add("Add_FileHint", "C:\\Jogos\\Jogo\\jogo.exe", "C:\\Games\\Game\\game.exe", "C:\\Juegos\\Juego\\juego.exe", "C:\\Jeux\\Jeu\\jeu.exe", "C:\\Spiele\\Spiel\\spiel.exe");
        Add("Add_Browse", "Escolher…", "Browse…", "Examinar…", "Parcourir…", "Durchsuchen…");
        Add("Add_FileFilter", "Programas e atalhos", "Programs and shortcuts", "Programas y accesos directos", "Programmes et raccourcis", "Programme und Verknüpfungen");
        Add("Add_AllFiles", "Todos os ficheiros", "All files", "Todos los archivos", "Tous les fichiers", "Alle Dateien");
        Add("Add_Name", "Nome", "Name", "Nombre", "Nom", "Name");
        Add("Add_NameHint", "Nome do jogo", "Game name", "Nombre del juego", "Nom du jeu", "Name des Spiels");
        Add("Add_Store", "Loja ou launcher", "Store or launcher", "Tienda o launcher", "Boutique ou launcher", "Store oder Launcher");
        Add("Add_StoreHint", "Ex.: Battle.net", "E.g. Battle.net", "P. ej.: Battle.net", "Ex. : Battle.net", "Z. B. Battle.net");
        Add("Add_Other", "Outros", "Other", "Otros", "Autres", "Andere");
        Add("Add_Args", "Argumentos (opcional)", "Arguments (optional)", "Argumentos (opcional)", "Arguments (facultatif)", "Argumente (optional)");
        Add("Add_ArgsHint", "Ex.: -fullscreen", "E.g. -fullscreen", "P. ej.: -fullscreen", "Ex. : -fullscreen", "Z. B. -fullscreen");
        Add("Add_Confirm", "Adicionar", "Add", "Añadir", "Ajouter", "Hinzufügen");
        Add("Add_ErrFile", "Escolhe um ficheiro que exista (ou escreve um link, ex.: battlenet://).",
            "Pick a file that exists (or type a link, e.g. battlenet://).",
            "Elige un archivo que exista (o escribe un enlace, p. ej. battlenet://).",
            "Choisis un fichier existant (ou saisis un lien, ex. battlenet://).",
            "Wähle eine vorhandene Datei (oder gib einen Link ein, z. B. battlenet://).");
        // Fechar um jogo à força
        Add("Game_Stop", "Parar", "Stop", "Detener", "Arrêter", "Beenden");
        Add("Game_StopConfirm",
            "Fechar {0} à força?\n\nUsa isto só se o jogo não fechar sozinho: o progresso que não estiver guardado perde-se.",
            "Force {0} to close?\n\nOnly use this if the game won't close on its own: any unsaved progress will be lost.",
            "¿Forzar el cierre de {0}?\n\nÚsalo solo si el juego no se cierra solo: se perderá el progreso no guardado.",
            "Forcer la fermeture de {0} ?\n\nÀ utiliser seulement si le jeu ne se ferme pas tout seul : la progression non sauvegardée sera perdue.",
            "{0} zwangsweise beenden?\n\nNur verwenden, wenn sich das Spiel nicht selbst schließt: nicht gespeicherter Fortschritt geht verloren.");
        Add("Game_StuckHint",
            "O jogo parece ter fechado, mas um processo dele continua aberto em segundo plano. O tempo de jogo já não está a contar. Usa \"Parar\" para o terminar.",
            "The game seems to have closed, but one of its processes is still running in the background. Playtime is no longer being counted. Use \"Stop\" to end it.",
            "El juego parece haberse cerrado, pero uno de sus procesos sigue abierto en segundo plano. El tiempo de juego ya no se cuenta. Usa \"Detener\" para terminarlo.",
            "Le jeu semble fermé, mais un de ses processus tourne encore en arrière-plan. Le temps de jeu n’est plus compté. Utilise « Arrêter » pour le fermer.",
            "Das Spiel scheint geschlossen, aber einer seiner Prozesse läuft noch im Hintergrund. Die Spielzeit wird nicht mehr gezählt. Mit „Beenden“ schließt du ihn.");

        // Imagens escolhidas à mão
        Add("Art_Customize", "Personalizar", "Customize", "Personalizar", "Personnaliser", "Anpassen");
        Add("Art_PageTitle", "Personalizar", "Customize", "Personalizar", "Personnaliser", "Anpassen");
        Add("Art_PageDesc",
            "Escolhe as tuas próprias imagens para este jogo. \"Repor\" volta à imagem encontrada automaticamente.",
            "Pick your own images for this game. \"Reset\" goes back to the one found automatically.",
            "Elige tus propias imágenes para este juego. \"Restablecer\" vuelve a la imagen encontrada automáticamente.",
            "Choisis tes propres images pour ce jeu. « Réinitialiser » revient à l’image trouvée automatiquement.",
            "Wähle eigene Bilder für dieses Spiel. „Zurücksetzen“ stellt das automatisch gefundene Bild wieder her.");
        Add("Art_Images", "Imagens", "Images", "Imágenes", "Images", "Bilder");
        Add("Art_AddDesc",
            "Opcional. O que não escolheres, a app tenta encontrar sozinha. Podes mudar tudo mais tarde em \"Personalizar\", na página do jogo.",
            "Optional. Anything you don't pick, the app tries to find on its own. You can change everything later with \"Customize\" on the game's page.",
            "Opcional. Lo que no elijas, la app intenta encontrarlo sola. Puedes cambiarlo todo más tarde en \"Personalizar\", en la página del juego.",
            "Facultatif. Ce que tu ne choisis pas, l’app essaie de le trouver toute seule. Tu peux tout changer plus tard avec « Personnaliser » sur la page du jeu.",
            "Optional. Was du nicht auswählst, versucht die App selbst zu finden. Du kannst später alles über „Anpassen“ auf der Spielseite ändern.");
        Add("Art_Cover", "Capa", "Cover", "Portada", "Jaquette", "Cover");
        Add("Art_CoverDesc", "Aparece nas filas e grelhas de jogos. Tamanho ideal: 600 × 900 píxeis.",
            "Shown in game rows and grids. Ideal size: 600 × 900 pixels.",
            "Aparece en las filas y cuadrículas de juegos. Tamaño ideal: 600 × 900 píxeles.",
            "Affichée dans les rangées et grilles de jeux. Taille idéale : 600 × 900 pixels.",
            "Erscheint in Spielreihen und -rastern. Ideale Größe: 600 × 900 Pixel.");
        Add("Art_Hero", "Plano de fundo", "Background", "Fondo", "Arrière-plan", "Hintergrund");
        Add("Art_HeroDesc", "Aparece no topo da página do jogo e no destaque do Início. Tamanho ideal: 3840 × 1240 píxeis.",
            "Shown at the top of the game page and in the Home spotlight. Ideal size: 3840 × 1240 pixels.",
            "Aparece en la parte superior de la página del juego y en el destacado de Inicio. Tamaño ideal: 3840 × 1240 píxeles.",
            "Affiché en haut de la page du jeu et dans la mise en avant de l’Accueil. Taille idéale : 3840 × 1240 pixels.",
            "Erscheint oben auf der Spielseite und im Highlight der Startseite. Ideale Größe: 3840 × 1240 Pixel.");
        Add("Art_Logo", "Logótipo", "Logo", "Logotipo", "Logo", "Logo");
        Add("Art_LogoDesc", "Aparece por cima do plano de fundo. Usa um PNG com fundo transparente. Tamanho ideal: 1280 píxeis de largura ou 720 de altura.",
            "Shown over the background. Use a PNG with a transparent background. Ideal size: 1280 pixels wide or 720 pixels tall.",
            "Aparece sobre el fondo. Usa un PNG con fondo transparente. Tamaño ideal: 1280 píxeles de ancho o 720 de alto.",
            "Affiché par-dessus l’arrière-plan. Utilise un PNG à fond transparent. Taille idéale : 1280 pixels de large ou 720 de haut.",
            "Erscheint über dem Hintergrund. Verwende ein PNG mit transparentem Hintergrund. Ideale Größe: 1280 Pixel breit oder 720 Pixel hoch.");
        Add("Art_Header", "Capa ampla", "Wide cover", "Portada ancha", "Jaquette large", "Breites Cover");
        Add("Art_HeaderDesc", "Usada no lugar do plano de fundo quando este falta. Tamanho ideal: 920 × 430 píxeis.",
            "Used instead of the background when there isn't one. Ideal size: 920 × 430 pixels.",
            "Se usa en lugar del fondo cuando falta. Tamaño ideal: 920 × 430 píxeles.",
            "Utilisée à la place de l’arrière-plan quand il manque. Taille idéale : 920 × 430 pixels.",
            "Wird statt des Hintergrunds verwendet, wenn keiner vorhanden ist. Ideale Größe: 920 × 430 Pixel.");
        Add("Art_Change", "Mudar", "Change", "Cambiar", "Changer", "Ändern");
        Add("Art_Reset", "Repor", "Reset", "Restablecer", "Réinitialiser", "Zurücksetzen");
        Add("Art_Custom", "Escolhida por ti", "Your pick", "Elegida por ti", "Choisie par toi", "Von dir gewählt");
        Add("Art_None", "Sem imagem", "No image", "Sin imagen", "Aucune image", "Kein Bild");
        Add("Art_Auto", "Automática", "Automatic", "Automática", "Automatique", "Automatisch");
        Add("Art_FileFilter", "Imagens", "Images", "Imágenes", "Images", "Bilder");
        Add("Art_ErrImage", "Não foi possível abrir essa imagem. Experimenta um ficheiro PNG ou JPG.",
            "Couldn't open that image. Try a PNG or JPG file.",
            "No se pudo abrir esa imagen. Prueba con un archivo PNG o JPG.",
            "Impossible d’ouvrir cette image. Essaie un fichier PNG ou JPG.",
            "Dieses Bild konnte nicht geöffnet werden. Versuch es mit einer PNG- oder JPG-Datei.");
        Add("Add_ErrName", "Escreve o nome do jogo.", "Type the game name.", "Escribe el nombre del juego.", "Saisis le nom du jeu.", "Gib den Namen des Spiels ein.");

        // ---------------- barra de ajuda do comando
        Add("Hint_Select", "Selecionar", "Select", "Seleccionar", "Valider", "Auswählen");
        Add("Hint_Back", "Voltar", "Back", "Atrás", "Retour", "Zurück");
        Add("Hint_Play", "Jogar", "Play", "Jugar", "Jouer", "Spielen");
        Add("Hint_Favorite", "Favorito", "Favorite", "Favorito", "Favori", "Favorit");
        Add("Hint_Fullscreen", "Ecrã inteiro", "Full screen", "Pantalla completa", "Plein écran", "Vollbild");
        Add("Hint_ExitFullscreen", "Sair do ecrã inteiro", "Exit full screen", "Salir de pantalla completa", "Quitter le plein écran", "Vollbild beenden");
        Add("Hint_Home", "Início", "Home", "Inicio", "Accueil", "Start");
        Add("Hint_Sections", "Secções", "Sections", "Secciones", "Sections", "Bereiche");

        // ---------------- início
        Add("Home_Continue", "CONTINUAR A JOGAR", "CONTINUE PLAYING", "SEGUIR JUGANDO", "CONTINUER À JOUER", "WEITERSPIELEN");
        Add("Home_ReadyToPlay", "Pronto a jogar", "Ready to play", "Listo para jugar", "Prêt à jouer", "Spielbereit");
        Add("Home_Empty",
            "Não foram encontrados jogos da Steam. Vai a Definições → Diagnóstico para ver onde a app procurou.",
            "No Steam games were found. Go to Settings → Diagnostics to see where the app looked.",
            "No se encontraron juegos de Steam. Ve a Ajustes → Diagnóstico para ver dónde buscó la app.",
            "Aucun jeu Steam trouvé. Va dans Paramètres → Diagnostic pour voir où l’app a cherché.",
            "Keine Steam-Spiele gefunden. Unter Einstellungen → Diagnose siehst du, wo gesucht wurde.");
        Add("SeeAll", "Ver todos", "See all", "Ver todos", "Tout voir", "Alle anzeigen");
        Add("Details", "Detalhes", "Details", "Detalles", "Détails", "Details");
        Add("FavoriteTooltip", "Favorito (Y / △)", "Favorite (Y / △)", "Favorito (Y / △)", "Favori (Y / △)", "Favorit (Y / △)");

        // ---------------- jogo (estado e textos curtos)
        Add("Play", "Jogar", "Play", "Jugar", "Jouer", "Spielen");
        Add("Playing", "A jogar", "Playing", "Jugando", "En jeu", "Läuft");
        Add("Launching", "A abrir…", "Starting…", "Abriendo…", "Lancement…", "Startet…");
        Add("NeverPlayed", "Nunca jogado", "Never played", "Nunca jugado", "Jamais joué", "Nie gespielt");
        Add("PlayedToday", "Jogado hoje", "Played today", "Jugado hoy", "Joué aujourd’hui", "Heute gespielt");
        Add("PlayedYesterday", "Jogado ontem", "Played yesterday", "Jugado ayer", "Joué hier", "Gestern gespielt");
        Add("PlayedDaysAgo", "Jogado há {0} dias", "Played {0} days ago", "Jugado hace {0} días", "Joué il y a {0} jours", "Vor {0} Tagen gespielt");
        Add("PlayedOn", "Jogado a {0}", "Played on {0}", "Jugado el {0}", "Joué le {0}", "Gespielt am {0}");
        Add("PlayedFor", "{0} jogadas", "{0} played", "{0} jugadas", "{0} de jeu", "{0} gespielt");

        // ---------------- biblioteca
        Add("Lib_DeleteCollection", "Apagar coleção", "Delete collection", "Eliminar colección", "Supprimer la collection", "Sammlung löschen");
        Add("Lib_Sort", "Ordenar", "Sort", "Ordenar", "Trier", "Sortieren");
        Add("Sort_Recent", "Recentes", "Recent", "Recientes", "Récents", "Zuletzt");
        Add("Sort_Name", "Nome", "Name", "Nombre", "Nom", "Name");
        Add("Sort_Playtime", "Mais jogados", "Most played", "Más jugados", "Plus joués", "Meistgespielt");
        Add("Sort_Size", "Tamanho", "Size", "Tamaño", "Taille", "Größe");
        Add("Lib_Collection", "Coleção", "Collection", "Colección", "Collection", "Sammlung");
        Add("Lib_OneGame", "1 jogo", "1 game", "1 juego", "1 jeu", "1 Spiel");
        Add("Lib_NGames", "{0} jogos", "{0} games", "{0} juegos", "{0} jeux", "{0} Spiele");
        Add("Lib_ForQuery", " para \"{0}\"", " for \"{0}\"", " para \"{0}\"", " pour « {0} »", " für „{0}“");
        Add("Lib_NoMatch", "Nenhum jogo corresponde à pesquisa.", "No games match your search.",
            "Ningún juego coincide con la búsqueda.", "Aucun jeu ne correspond à la recherche.", "Keine Spiele passen zur Suche.");
        Add("Lib_NoFavorites",
            "Ainda não tens favoritos. Abre um jogo e carrega no coração (ou Y / △ no comando).",
            "You don't have any favorites yet. Open a game and press the heart (or Y / △ on the controller).",
            "Aún no tienes favoritos. Abre un juego y pulsa el corazón (o Y / △ en el mando).",
            "Tu n’as pas encore de favoris. Ouvre un jeu et appuie sur le cœur (ou Y / △ sur la manette).",
            "Du hast noch keine Favoriten. Öffne ein Spiel und drücke das Herz (oder Y / △ am Controller).");
        Add("Lib_EmptyCollection",
            "Esta coleção está vazia. Abre um jogo e escolhe esta coleção na secção \"Coleções\".",
            "This collection is empty. Open a game and pick this collection under \"Collections\".",
            "Esta colección está vacía. Abre un juego y elige esta colección en \"Colecciones\".",
            "Cette collection est vide. Ouvre un jeu et choisis-la dans « Collections ».",
            "Diese Sammlung ist leer. Öffne ein Spiel und wähle sie unter „Sammlungen“.");
        Add("Lib_NoGames", "Nenhum jogo encontrado.", "No games found.", "No se encontraron juegos.", "Aucun jeu trouvé.", "Keine Spiele gefunden.");
        Add("Lib_DeleteConfirm",
            "Apagar a coleção \"{0}\"? Os jogos não são afetados.",
            "Delete the collection \"{0}\"? Your games won't be affected.",
            "¿Eliminar la colección \"{0}\"? Los juegos no se ven afectados.",
            "Supprimer la collection « {0} » ? Les jeux ne sont pas touchés.",
            "Sammlung „{0}“ löschen? Die Spiele bleiben erhalten.");

        // ---------------- página do jogo
        Add("Game_Folder", "Pasta", "Folder", "Carpeta", "Dossier", "Ordner");
        Add("Game_PlaytimeLabel", "TEMPO JOGADO", "TIME PLAYED", "TIEMPO JUGADO", "TEMPS DE JEU", "SPIELZEIT");
        Add("Game_LastLabel", "ÚLTIMA VEZ", "LAST PLAYED", "ÚLTIMA VEZ", "DERNIÈRE FOIS", "ZULETZT");
        Add("Game_SizeLabel", "TAMANHO", "SIZE", "TAMAÑO", "TAILLE", "GRÖSSE");
        Add("Game_StoreLabel", "LOJA", "STORE", "TIENDA", "BOUTIQUE", "STORE");
        Add("Game_Remove", "Remover", "Remove", "Quitar", "Retirer", "Entfernen");
        Add("Game_RemoveConfirm", "Remover \"{0}\" da biblioteca? Os ficheiros do jogo não são apagados.",
            "Remove \"{0}\" from the library? The game files won't be deleted.",
            "¿Quitar \"{0}\" de la biblioteca? Los archivos del juego no se borran.",
            "Retirer « {0} » de la bibliothèque ? Les fichiers du jeu ne sont pas supprimés.",
            "„{0}“ aus der Bibliothek entfernen? Die Spieldateien werden nicht gelöscht.");
        Add("Game_NewCollectionHint", "Nome da nova coleção", "New collection name", "Nombre de la nueva colección", "Nom de la nouvelle collection", "Name der neuen Sammlung");
        Add("Game_CreateAndAdd", "Criar e adicionar", "Create and add", "Crear y añadir", "Créer et ajouter", "Erstellen und hinzufügen");
        Add("Game_RecentSessions", "Sessões recentes", "Recent sessions", "Sesiones recientes", "Sessions récentes", "Letzte Sitzungen");
        Add("Game_NoSessions",
            "Ainda não há sessões registadas. As sessões são contadas enquanto esta app estiver aberta, mesmo que abras o jogo pela Steam.",
            "No sessions recorded yet. Sessions are counted while this app is open, even if you start the game from Steam.",
            "Aún no hay sesiones registradas. Las sesiones se cuentan mientras esta app esté abierta, aunque abras el juego desde Steam.",
            "Aucune session enregistrée. Les sessions sont comptées tant que cette app est ouverte, même si tu lances le jeu depuis Steam.",
            "Noch keine Sitzungen erfasst. Sitzungen werden gezählt, solange diese App geöffnet ist – auch wenn du das Spiel über Steam startest.");
        Add("Game_NoCollections",
            "Ainda não tens coleções. Cria uma em baixo (ex.: \"A jogar\", \"Multijogador\").",
            "You don't have any collections yet. Create one below (e.g. \"Playing\", \"Multiplayer\").",
            "Aún no tienes colecciones. Crea una abajo (p. ej., \"Jugando\", \"Multijugador\").",
            "Tu n’as pas encore de collections. Crées-en une ci-dessous (ex. « En cours », « Multijoueur »).",
            "Du hast noch keine Sammlungen. Erstelle unten eine (z. B. „Am Spielen“, „Mehrspieler“).");

        // ---------------- tempo de jogo
        Add("Pt_Subtitle",
            "O total vem da Steam (e da Epic, se ligares a conta em Definições). O gráfico dos últimos dias conta as sessões registadas enquanto esta app está aberta.",
            "The total comes from Steam (and Epic, if you connect your account in Settings). The chart counts sessions recorded while this app is open.",
            "El total viene de Steam (y de Epic, si conectas la cuenta en Ajustes). El gráfico cuenta las sesiones registradas mientras esta app está abierta.",
            "Le total vient de Steam (et d’Epic, si tu connectes ton compte dans Paramètres). Le graphique compte les sessions enregistrées pendant que cette app est ouverte.",
            "Die Gesamtzeit stammt von Steam (und Epic, wenn du dein Konto in den Einstellungen verbindest). Das Diagramm zählt Sitzungen, die bei geöffneter App erfasst wurden.");
        Add("Pt_Total", "TOTAL", "TOTAL", "TOTAL", "TOTAL", "GESAMT");
        Add("Pt_TotalSub", "em todos os jogos instalados", "across all installed games", "en todos los juegos instalados", "sur tous les jeux installés", "über alle installierten Spiele");
        Add("Pt_Last7Upper", "ÚLTIMOS 7 DIAS", "LAST 7 DAYS", "ÚLTIMOS 7 DÍAS", "7 DERNIERS JOURS", "LETZTE 7 TAGE");
        Add("Pt_MostPlayedUpper", "MAIS JOGADO", "MOST PLAYED", "MÁS JUGADO", "LE PLUS JOUÉ", "AM MEISTEN GESPIELT");
        Add("Pt_Last7", "Últimos 7 dias", "Last 7 days", "Últimos 7 días", "7 derniers jours", "Letzte 7 Tage");
        Add("Pt_TopGames", "Jogos mais jogados", "Most played games", "Juegos más jugados", "Jeux les plus joués", "Meistgespielte Spiele");
        Add("Pt_None", "Ainda não há tempo de jogo registado.", "No playtime recorded yet.", "Aún no hay tiempo de juego registrado.", "Aucun temps de jeu enregistré.", "Noch keine Spielzeit erfasst.");
        Add("Pt_OneSession", "1 sessão", "1 session", "1 sesión", "1 session", "1 Sitzung");
        Add("Pt_NSessions", "{0} sessões", "{0} sessions", "{0} sesiones", "{0} sessions", "{0} Sitzungen");
        Add("Today", "Hoje", "Today", "Hoy", "Aujourd’hui", "Heute");
        Add("Pt_NoSessionsDay", "Sem sessões", "No sessions", "Sin sesiones", "Aucune session", "Keine Sitzungen");

        // ---------------- definições
        Add("Set_Language", "Idioma", "Language", "Idioma", "Langue", "Sprache");
        Add("Set_LanguageDesc", "Muda o idioma de toda a app.", "Changes the language of the whole app.", "Cambia el idioma de toda la app.", "Change la langue de toute l’app.", "Ändert die Sprache der gesamten App.");
        Add("Set_EpicTitle", "Conta Epic Games — só tempo de jogo", "Epic Games account — playtime only", "Cuenta de Epic Games: solo tiempo de juego", "Compte Epic Games — temps de jeu uniquement", "Epic-Games-Konto – nur Spielzeit");
        Add("Set_EpicDesc",
            "A Epic não guarda o tempo de jogo no PC, só na tua conta. Se quiseres, liga a conta para a app ir buscar essas horas.",
            "Epic doesn't keep playtime on your PC, only in your account. If you want, connect your account so the app can fetch those hours.",
            "Epic no guarda el tiempo de juego en el PC, solo en tu cuenta. Si quieres, conecta la cuenta para que la app obtenga esas horas.",
            "Epic ne garde pas le temps de jeu sur le PC, seulement sur ton compte. Si tu veux, connecte ton compte pour que l’app récupère ces heures.",
            "Epic speichert die Spielzeit nicht auf dem PC, nur in deinem Konto. Wenn du möchtest, verbinde dein Konto, damit die App diese Stunden abruft.");
        Add("Set_EpicOnly", "Serve apenas para ler o tempo de jogo dos teus jogos da Epic.", "It is only used to read the playtime of your Epic games.",
            "Solo se usa para leer el tiempo de juego de tus juegos de Epic.", "Sert uniquement à lire le temps de jeu de tes jeux Epic.", "Dient ausschließlich dazu, die Spielzeit deiner Epic-Spiele zu lesen.");
        Add("Set_EpicNoPassword", "O login é feito no site da Epic: a app nunca vê a tua palavra-passe.", "You sign in on Epic's website: the app never sees your password.",
            "El inicio de sesión se hace en la web de Epic: la app nunca ve tu contraseña.", "La connexion se fait sur le site d’Epic : l’app ne voit jamais ton mot de passe.", "Die Anmeldung erfolgt auf der Epic-Website: Die App sieht dein Passwort nie.");
        Add("Set_EpicStored", "A autorização fica guardada encriptada só neste PC. Podes desligar quando quiseres.", "The authorization is stored encrypted on this PC only. You can disconnect at any time.",
            "La autorización se guarda cifrada solo en este PC. Puedes desconectar cuando quieras.", "L’autorisation est enregistrée chiffrée sur ce PC uniquement. Tu peux te déconnecter à tout moment.", "Die Autorisierung wird verschlüsselt nur auf diesem PC gespeichert. Du kannst sie jederzeit trennen.");
        Add("Set_EpicNoChanges", "Não compra, não descarrega, nem altera nada na tua conta.", "It doesn't buy, download or change anything in your account.",
            "No compra, no descarga ni cambia nada en tu cuenta.", "N’achète rien, ne télécharge rien et ne modifie rien sur ton compte.", "Es wird nichts gekauft, heruntergeladen oder in deinem Konto geändert.");
        Add("Set_EpicUnofficial", "Ligação não oficial (a mesma que o Heroic Launcher usa): pode deixar de funcionar se a Epic a mudar.",
            "Unofficial connection (the same one Heroic Launcher uses): it may stop working if Epic changes it.",
            "Conexión no oficial (la misma que usa Heroic Launcher): puede dejar de funcionar si Epic la cambia.",
            "Connexion non officielle (la même que Heroic Launcher) : elle peut cesser de fonctionner si Epic la modifie.",
            "Inoffizielle Verbindung (dieselbe wie beim Heroic Launcher): Sie kann aufhören zu funktionieren, wenn Epic sie ändert.");
        Add("Set_EpicWarning",
            "A página da Epic avisa que o código dá acesso total à conta: é o mesmo tipo de autorização que o launcher da Epic usa. Esta app só a usa para ler o tempo de jogo e não a envia a mais ninguém. Liga só se confiares nesta app.",
            "Epic's page warns that the code gives full access to the account: it's the same kind of authorization the Epic launcher uses. This app only uses it to read playtime and never sends it anywhere else. Only connect if you trust this app.",
            "La página de Epic avisa de que el código da acceso total a la cuenta: es el mismo tipo de autorización que usa el launcher de Epic. Esta app solo la usa para leer el tiempo de juego y no la envía a nadie más. Conecta solo si confías en esta app.",
            "La page d’Epic prévient que le code donne un accès complet au compte : c’est le même type d’autorisation que celle du launcher Epic. Cette app l’utilise uniquement pour lire le temps de jeu et ne l’envoie à personne d’autre. Connecte-toi seulement si tu fais confiance à cette app.",
            "Die Epic-Seite warnt, dass der Code vollen Kontozugriff gewährt: Es ist dieselbe Art von Autorisierung, die der Epic-Launcher nutzt. Diese App verwendet sie nur zum Lesen der Spielzeit und sendet sie nirgendwohin. Verbinde nur, wenn du dieser App vertraust.");
        Add("Set_EpicStep1", "1. Abre a página de login da Epic e entra na tua conta.", "1. Open Epic's sign-in page and sign in to your account.",
            "1. Abre la página de inicio de sesión de Epic y entra en tu cuenta.", "1. Ouvre la page de connexion d’Epic et connecte-toi.", "1. Öffne die Epic-Anmeldeseite und melde dich an.");
        Add("Set_EpicOpenLogin", "Abrir login da Epic", "Open Epic sign-in", "Abrir inicio de sesión de Epic", "Ouvrir la connexion Epic", "Epic-Anmeldung öffnen");
        Add("Set_EpicStep2",
            "2. Depois de entrares aparece uma página só com texto. Copia tudo (Ctrl+A, Ctrl+C) e volta aqui — o código é colado sozinho. Depois carrega em Ligar.",
            "2. After signing in you'll see a page with only text. Copy everything (Ctrl+A, Ctrl+C) and come back here — the code is pasted automatically. Then press Connect.",
            "2. Tras iniciar sesión verás una página solo con texto. Cópialo todo (Ctrl+A, Ctrl+C) y vuelve aquí: el código se pega solo. Después pulsa Conectar.",
            "2. Après la connexion, une page avec seulement du texte s’affiche. Copie tout (Ctrl+A, Ctrl+C) et reviens ici : le code est collé automatiquement. Puis appuie sur Connecter.",
            "2. Nach der Anmeldung erscheint eine Seite nur mit Text. Kopiere alles (Strg+A, Strg+C) und kehre hierher zurück – der Code wird automatisch eingefügt. Dann auf Verbinden drücken.");
        Add("Set_EpicCodeHint", "Código (authorizationCode)", "Code (authorizationCode)", "Código (authorizationCode)", "Code (authorizationCode)", "Code (authorizationCode)");
        Add("Set_EpicConnect", "Ligar", "Connect", "Conectar", "Connecter", "Verbinden");
        Add("Set_EpicConnecting", "A ligar…", "Connecting…", "Conectando…", "Connexion…", "Verbinde…");
        Add("Set_EpicBadCode", "Não encontrei um código válido. Copia o texto todo da página da Epic depois do login.",
            "No valid code found. Copy all the text from Epic's page after signing in.",
            "No se encontró un código válido. Copia todo el texto de la página de Epic tras iniciar sesión.",
            "Aucun code valide trouvé. Copie tout le texte de la page Epic après la connexion.",
            "Kein gültiger Code gefunden. Kopiere nach der Anmeldung den gesamten Text der Epic-Seite.");
        Add("Set_EpicError", "A Epic recusou a ligação: {0} (o código só dura uns minutos — tenta de novo).",
            "Epic refused the connection: {0} (the code only lasts a few minutes — try again).",
            "Epic rechazó la conexión: {0} (el código solo dura unos minutos; inténtalo de nuevo).",
            "Epic a refusé la connexion : {0} (le code ne dure que quelques minutes — réessaie).",
            "Epic hat die Verbindung abgelehnt: {0} (der Code gilt nur wenige Minuten – versuche es erneut).");
        Add("Set_EpicConnectedAs", "Ligado como {0}", "Connected as {0}", "Conectado como {0}", "Connecté en tant que {0}", "Verbunden als {0}");
        Add("Set_EpicLastSync", "Tempo de jogo atualizado: {0}", "Playtime updated: {0}", "Tiempo de juego actualizado: {0}", "Temps de jeu mis à jour : {0}", "Spielzeit aktualisiert: {0}");
        Add("Set_EpicNeverSynced", "Tempo de jogo ainda não atualizado.", "Playtime not updated yet.", "Tiempo de juego aún no actualizado.", "Temps de jeu pas encore mis à jour.", "Spielzeit noch nicht aktualisiert.");
        Add("Set_EpicSync", "Atualizar tempo de jogo", "Update playtime", "Actualizar tiempo de juego", "Mettre à jour le temps de jeu", "Spielzeit aktualisieren");
        Add("Set_EpicDisconnect", "Desligar conta", "Disconnect account", "Desconectar cuenta", "Déconnecter le compte", "Konto trennen");
        Add("Set_EpicDisconnected", "Conta desligada e autorização apagada deste PC.", "Account disconnected and authorization deleted from this PC.",
            "Cuenta desconectada y autorización borrada de este PC.", "Compte déconnecté et autorisation supprimée de ce PC.", "Konto getrennt und Autorisierung von diesem PC gelöscht.");
        Add("Set_EpicCodeDetected", "Código detetado. Carrega em Ligar.", "Code detected. Press Connect.", "Código detectado. Pulsa Conectar.", "Code détecté. Appuie sur Connecter.", "Code erkannt. Drücke auf Verbinden.");
        Add("Set_EpicSyncing", "A ler o tempo de jogo…", "Reading playtime…", "Leyendo el tiempo de juego…", "Lecture du temps de jeu…", "Spielzeit wird gelesen…");
        Add("Set_EpicSynced", "Tempo de jogo da Epic atualizado.", "Epic playtime updated.", "Tiempo de juego de Epic actualizado.", "Temps de jeu Epic mis à jour.", "Epic-Spielzeit aktualisiert.");
        Add("Set_EpicSyncFailed", "Não foi possível ler o tempo de jogo. Se continuar, desliga e volta a ligar a conta.",
            "Couldn't read playtime. If it keeps failing, disconnect and connect the account again.",
            "No se pudo leer el tiempo de juego. Si sigue fallando, desconecta y vuelve a conectar la cuenta.",
            "Impossible de lire le temps de jeu. Si ça continue, déconnecte puis reconnecte le compte.",
            "Spielzeit konnte nicht gelesen werden. Wenn das weiter passiert, trenne das Konto und verbinde es erneut.");
        Add("Set_Appearance", "Aparência", "Appearance", "Apariencia", "Apparence", "Darstellung");
        Add("Set_AppearanceDesc", "Cor de fundo da app.", "App background color.", "Color de fondo de la app.", "Couleur de fond de l’app.", "Hintergrundfarbe der App.");
        Add("Set_UseGameColors", "Usar a cor de cada jogo no Início e na página do jogo", "Use each game's color on Home and game pages",
            "Usar el color de cada juego en Inicio y en la página del juego", "Utiliser la couleur de chaque jeu sur l’accueil et la page du jeu", "Farbe des jeweiligen Spiels auf Start- und Spielseite verwenden");
        Add("Color_Grey", "Cinzento", "Gray", "Gris", "Gris", "Grau");
        Add("Color_Red", "Vermelho", "Red", "Rojo", "Rouge", "Rot");
        Add("Color_Blue", "Azul", "Blue", "Azul", "Bleu", "Blau");
        Add("Color_Purple", "Roxo", "Purple", "Morado", "Violet", "Lila");
        Add("Color_Green", "Verde", "Green", "Verde", "Vert", "Grün");
        Add("Set_SgdbTitle", "Capas do SteamGridDB", "SteamGridDB artwork", "Portadas de SteamGridDB", "Jaquettes SteamGridDB", "SteamGridDB-Cover");
        Add("Set_SgdbDesc",
            "Usado quando a Steam não tem imagem para um jogo. A chave fica guardada só neste PC.",
            "Used when Steam has no image for a game. The key is stored only on this PC.",
            "Se usa cuando Steam no tiene imagen para un juego. La clave se guarda solo en este PC.",
            "Utilisé quand Steam n’a pas d’image pour un jeu. La clé est enregistrée uniquement sur ce PC.",
            "Wird genutzt, wenn Steam kein Bild für ein Spiel hat. Der Schlüssel wird nur auf diesem PC gespeichert.");
        Add("Set_SgdbHint", "Cola aqui a chave da API", "Paste your API key here", "Pega aquí la clave de la API", "Colle ta clé API ici", "API-Schlüssel hier einfügen");
        Add("Save", "Guardar", "Save", "Guardar", "Enregistrer", "Speichern");
        Add("Set_NoKey", "Sem chave configurada.", "No key set.", "No hay clave configurada.", "Aucune clé configurée.", "Kein Schlüssel festgelegt.");
        Add("Set_KeySet", "Chave configurada.", "Key set.", "Clave configurada.", "Clé configurée.", "Schlüssel festgelegt.");
        Add("Set_KeyRemoved", "Chave removida.", "Key removed.", "Clave eliminada.", "Clé supprimée.", "Schlüssel entfernt.");
        Add("Set_KeySaved", "Chave guardada. A procurar imagens em falta…", "Key saved. Looking for missing images…",
            "Clave guardada. Buscando imágenes que faltan…", "Clé enregistrée. Recherche des images manquantes…", "Schlüssel gespeichert. Fehlende Bilder werden gesucht…");
        Add("Set_Images", "Imagens", "Images", "Imágenes", "Images", "Bilder");
        Add("Set_RetryArt", "Procurar imagens em falta", "Find missing images", "Buscar imágenes que faltan", "Chercher les images manquantes", "Fehlende Bilder suchen");
        Add("Set_ClearCache", "Limpar cache e recalcular cores", "Clear cache and recalculate colors", "Vaciar caché y recalcular colores", "Vider le cache et recalculer les couleurs", "Cache leeren und Farben neu berechnen");
        Add("Set_CacheInfo",
            "{0} de {1} jogos têm capa. Cache da app: {2:0.0} MB (as imagens que já existem na cache da Steam não são copiadas).",
            "{0} of {1} games have a cover. App cache: {2:0.0} MB (images already in Steam's cache aren't copied).",
            "{0} de {1} juegos tienen portada. Caché de la app: {2:0.0} MB (las imágenes que ya están en la caché de Steam no se copian).",
            "{0} jeux sur {1} ont une jaquette. Cache de l’app : {2:0.0} Mo (les images déjà dans le cache de Steam ne sont pas copiées).",
            "{0} von {1} Spielen haben ein Cover. App-Cache: {2:0.0} MB (Bilder aus dem Steam-Cache werden nicht kopiert).");
        Add("Set_Searching", "A procurar imagens em falta…", "Looking for missing images…", "Buscando imágenes que faltan…", "Recherche des images manquantes…", "Fehlende Bilder werden gesucht…");
        Add("Set_SearchingProgress", "A procurar imagens… {0} de {1} jogos", "Looking for images… {0} of {1} games",
            "Buscando imágenes… {0} de {1} juegos", "Recherche des images… {0} jeux sur {1}", "Bilder werden gesucht … {0} von {1} Spielen");
        Add("Set_FoundImages", "Encontradas {0} imagens novas.", "Found {0} new images.", "Se encontraron {0} imágenes nuevas.",
            "{0} nouvelles images trouvées.", "{0} neue Bilder gefunden.");
        Add("Set_NoNewImages",
            "Não foram encontradas imagens novas. Podes escolher as tuas em \"Personalizar\", na página de cada jogo.",
            "No new images found. You can pick your own with \"Customize\" on each game's page.",
            "No se encontraron imágenes nuevas. Puedes elegir las tuyas en \"Personalizar\", en la página de cada juego.",
            "Aucune nouvelle image trouvée. Tu peux choisir les tiennes avec « Personnaliser » sur la page de chaque jeu.",
            "Keine neuen Bilder gefunden. Du kannst eigene über „Anpassen“ auf der Seite jedes Spiels wählen.");
        Add("Set_Screen", "Ecrã", "Display", "Pantalla", "Affichage", "Anzeige");
        Add("Set_ScreenDesc", "Útil para usar na TV ou só com o comando.", "Handy on a TV or when using only a controller.",
            "Útil en la TV o usando solo el mando.", "Pratique sur une TV ou avec une manette uniquement.", "Praktisch am Fernseher oder nur mit Controller.");
        Add("Set_StartFullscreen", "Abrir sempre em ecrã inteiro", "Always open in full screen", "Abrir siempre en pantalla completa", "Toujours ouvrir en plein écran", "Immer im Vollbild öffnen");
        Add("Set_Input", "Teclado e comando", "Keyboard and controller", "Teclado y mando", "Clavier et manette", "Tastatur und Controller");
        Add("Set_InputDesc",
            "Funcionam comandos Xbox e PlayStation (DualShock 4 e DualSense), por cabo ou Bluetooth.",
            "Xbox and PlayStation controllers (DualShock 4 and DualSense) work over USB or Bluetooth.",
            "Funcionan mandos de Xbox y PlayStation (DualShock 4 y DualSense), por cable o Bluetooth.",
            "Les manettes Xbox et PlayStation (DualShock 4 et DualSense) fonctionnent en USB ou Bluetooth.",
            "Xbox- und PlayStation-Controller (DualShock 4 und DualSense) funktionieren per USB oder Bluetooth.");
        Add("Set_Controllers", "Comandos ligados agora: {0}", "Controllers connected now: {0}", "Mandos conectados ahora: {0}", "Manettes connectées : {0}", "Jetzt verbundene Controller: {0}");
        Add("Set_NoControllers", "nenhum", "none", "ninguno", "aucune", "keine");
        Add("Set_Diagnostics", "Diagnóstico", "Diagnostics", "Diagnóstico", "Diagnostic", "Diagnose");
        Add("Set_Reload", "Atualizar biblioteca (F5)", "Refresh library (F5)", "Actualizar biblioteca (F5)", "Actualiser la bibliothèque (F5)", "Bibliothek aktualisieren (F5)");
        Add("Set_AppData", "Dados da app: {0}", "App data: {0}", "Datos de la app: {0}", "Données de l’app : {0}", "App-Daten: {0}");

        // ---------------- sobre / créditos
        Add("About_Version", "Versão {0}", "Version {0}", "Versión {0}", "Version {0}", "Version {0}");
        Add("About_MadeBy", "Criado por {0}", "Created by {0}", "Creado por {0}", "Créé par {0}", "Erstellt von {0}");
        Add("About_Credits", "Créditos", "Credits", "Créditos", "Crédits", "Danksagungen");
        Add("About_Images", "Imagens dos jogos: Steam (© Valve e editores) e arte da comunidade do", "Game artwork: Steam (© Valve and publishers) and community art from",
            "Imágenes de los juegos: Steam (© Valve y editores) y arte de la comunidad de", "Images des jeux : Steam (© Valve et éditeurs) et art de la communauté de", "Spielbilder: Steam (© Valve und Publisher) und Community-Grafiken von");
        Add("About_Hid", "Comandos PlayStation:", "PlayStation controllers:", "Mandos de PlayStation:", "Manettes PlayStation :", "PlayStation-Controller:");
        Add("About_Icons", "Ícones dos botões desenhados para esta app, com estilo inspirado em", "Button icons drawn for this app, style inspired by",
            "Iconos de botones dibujados para esta app, con estilo inspirado en", "Icônes des boutons dessinées pour cette app, style inspiré de", "Tasten-Icons für diese App gezeichnet, Stil inspiriert von");
        Add("About_Dotnet", "Feito com", "Made with", "Hecho con", "Fait avec", "Erstellt mit");
        Add("About_Trademarks",
            "Steam, Epic Games, Ubisoft Connect, Xbox, PlayStation, DualShock e DualSense são marcas dos respetivos donos. O MyGameLauncher não é afiliado nem aprovado por nenhuma destas empresas.",
            "Steam, Epic Games, Ubisoft Connect, Xbox, PlayStation, DualShock and DualSense are trademarks of their respective owners. MyGameLauncher is not affiliated with or endorsed by any of these companies.",
            "Steam, Epic Games, Ubisoft Connect, Xbox, PlayStation, DualShock y DualSense son marcas de sus respectivos propietarios. MyGameLauncher no está afiliado ni aprobado por ninguna de estas empresas.",
            "Steam, Epic Games, Ubisoft Connect, Xbox, PlayStation, DualShock et DualSense sont des marques de leurs propriétaires respectifs. MyGameLauncher n’est affilié à aucune de ces sociétés ni approuvé par elles.",
            "Steam, Epic Games, Ubisoft Connect, Xbox, PlayStation, DualShock und DualSense sind Marken ihrer jeweiligen Inhaber. MyGameLauncher ist mit keinem dieser Unternehmen verbunden und wird von keinem unterstützt.");
        Add("About_Licenses", "Ver licenças", "View licenses", "Ver licencias", "Voir les licences", "Lizenzen anzeigen");
        Add("About_NoLicenseFile", "O ficheiro de licenças (THIRD-PARTY-NOTICES.txt) não está na pasta da app.", "The license file (THIRD-PARTY-NOTICES.txt) isn't in the app folder.",
            "El archivo de licencias (THIRD-PARTY-NOTICES.txt) no está en la carpeta de la app.", "Le fichier de licences (THIRD-PARTY-NOTICES.txt) n’est pas dans le dossier de l’app.", "Die Lizenzdatei (THIRD-PARTY-NOTICES.txt) befindet sich nicht im App-Ordner.");

        // ---------------- tabela de atalhos
        Add("Sh_Action", "Ação", "Action", "Acción", "Action", "Aktion");
        Add("Sh_Keyboard", "Teclado", "Keyboard", "Teclado", "Clavier", "Tastatur");
        Add("Sh_Arrows", "Setas", "Arrow keys", "Flechas", "Flèches", "Pfeiltasten");
        Add("Sh_Move", "Mover entre elementos", "Move around", "Moverse", "Se déplacer", "Navigieren");
        Add("Sh_Open", "Abrir / carregar", "Open / select", "Abrir / seleccionar", "Ouvrir / valider", "Öffnen / auswählen");
        Add("Sh_Back", "Voltar", "Back", "Atrás", "Retour", "Zurück");
        Add("Sh_PlaySelected", "Jogar o jogo selecionado", "Play selected game", "Jugar al juego seleccionado", "Jouer au jeu sélectionné", "Ausgewähltes Spiel spielen");
        Add("Sh_Favorite", "Favorito", "Favorite", "Favorito", "Favori", "Favorit");
        Add("Sh_Sections", "Secção anterior / seguinte", "Previous / next section", "Sección anterior / siguiente", "Section précédente / suivante", "Vorheriger / nächster Bereich");
        Add("Sh_Home", "Ir para o Início", "Go to Home", "Ir a Inicio", "Aller à l’accueil", "Zum Start");
        Add("Sh_Search", "Pesquisar", "Search", "Buscar", "Rechercher", "Suchen");
        Add("Sh_Reload", "Atualizar biblioteca", "Refresh library", "Actualizar biblioteca", "Actualiser la bibliothèque", "Bibliothek aktualisieren");

        // ---------------- mensagens
        Add("Err_Title", "Erro", "Error", "Error", "Erreur", "Fehler");
        Add("Err_Friendly",
            "Algo correu mal, mas a app continua a funcionar.\n\nOs detalhes ficaram guardados no ficheiro de registo. Se voltar a acontecer, podes enviar esse ficheiro ao criador da app.\n\nQueres abrir a pasta do registo?",
            "Something went wrong, but the app is still running.\n\nThe details were saved to the log file. If it happens again, you can send that file to the app's creator.\n\nOpen the log folder?",
            "Algo salió mal, pero la app sigue funcionando.\n\nLos detalles se guardaron en el archivo de registro. Si vuelve a pasar, puedes enviar ese archivo al creador de la app.\n\n¿Abrir la carpeta del registro?",
            "Un problème est survenu, mais l’app fonctionne toujours.\n\nLes détails ont été enregistrés dans le fichier journal. Si cela se reproduit, tu peux envoyer ce fichier au créateur de l’app.\n\nOuvrir le dossier du journal ?",
            "Etwas ist schiefgelaufen, aber die App läuft weiter.\n\nDie Details wurden in der Protokolldatei gespeichert. Wenn es wieder passiert, kannst du diese Datei an den Entwickler der App senden.\n\nProtokollordner öffnen?");

        // ---------------- Windows / área de notificação
        Add("Set_Windows", "Windows", "Windows", "Windows", "Windows", "Windows");
        Add("Set_WindowsDesc",
            "Com a app a correr em segundo plano, o tempo de jogo continua a ser contado mesmo quando abres os jogos diretamente na Steam ou na Epic.",
            "With the app running in the background, playtime keeps being counted even when you open games straight from Steam or Epic.",
            "Con la app en segundo plano, el tiempo de juego se sigue contando aunque abras los juegos directamente desde Steam o Epic.",
            "Avec l’app en arrière-plan, le temps de jeu continue d’être compté même si tu lances les jeux directement depuis Steam ou Epic.",
            "Läuft die App im Hintergrund, wird die Spielzeit weiter gezählt, auch wenn du Spiele direkt über Steam oder Epic startest.");
        Add("Set_StartWithWindows", "Abrir com o Windows (na área de notificação)", "Start with Windows (in the notification area)",
            "Abrir con Windows (en el área de notificación)", "Lancer avec Windows (dans la zone de notification)", "Mit Windows starten (im Infobereich)");
        Add("Set_CloseToTray", "Ao fechar, continuar na área de notificação", "Keep running in the notification area when closed",
            "Al cerrar, seguir en el área de notificación", "À la fermeture, rester dans la zone de notification", "Beim Schließen im Infobereich weiterlaufen");
        Add("Tray_Open", "Abrir MyGameLauncher", "Open MyGameLauncher", "Abrir MyGameLauncher", "Ouvrir MyGameLauncher", "MyGameLauncher öffnen");
        Add("Tray_Exit", "Sair", "Exit", "Salir", "Quitter", "Beenden");
        Add("Tray_Hint",
            "A app continua aqui, a contar o tempo de jogo. Para sair, usa o botão direito neste ícone.",
            "The app is still here, counting your playtime. To quit, right-click this icon.",
            "La app sigue aquí, contando el tiempo de juego. Para salir, haz clic derecho en este icono.",
            "L’app est toujours là et compte ton temps de jeu. Pour quitter, fais un clic droit sur cette icône.",
            "Die App läuft hier weiter und zählt deine Spielzeit. Zum Beenden mit der rechten Maustaste auf dieses Symbol klicken.");
        Add("Set_OpenLogs", "Abrir pasta dos registos", "Open log folder", "Abrir carpeta de registros", "Ouvrir le dossier des journaux", "Protokollordner öffnen");

        // ---------------- atualizações
        Add("Upd_Available", "Versão nova", "New version", "Nueva versión", "Nouvelle version", "Neue Version");
        Add("Upd_Version", "Disponível: {0} (tens a {1})", "Available: {0} (you have {1})", "Disponible: {0} (tienes la {1})",
            "Disponible : {0} (tu as la {1})", "Verfügbar: {0} (du hast {1})");
        Add("Upd_Download", "Ver e transferir", "View and download", "Ver y descargar", "Voir et télécharger", "Ansehen und herunterladen");
        Add("Upd_Dismiss", "Não avisar desta versão", "Don't remind me about this version", "No avisar de esta versión",
            "Ne plus me prévenir pour cette version", "Für diese Version nicht mehr erinnern");
        Add("Upd_CheckNow", "Procurar atualizações", "Check for updates", "Buscar actualizaciones", "Rechercher des mises à jour", "Nach Updates suchen");
        Add("Upd_Auto", "Procurar ao abrir a app", "Check when the app opens", "Buscar al abrir la app", "Vérifier à l’ouverture", "Beim Start prüfen");
        Add("Upd_Checking", "A procurar…", "Checking…", "Buscando…", "Recherche…", "Wird geprüft …");
        Add("Upd_UpToDate", "Tens a versão mais recente ({0}).", "You have the latest version ({0}).", "Tienes la versión más reciente ({0}).",
            "Tu as la dernière version ({0}).", "Du hast die neueste Version ({0}).");
        Add("Upd_Failed", "Não foi possível ver se há atualizações. Confirma a ligação à internet.",
            "Couldn't check for updates. Check your internet connection.",
            "No se pudo comprobar si hay actualizaciones. Revisa tu conexión a internet.",
            "Impossible de vérifier les mises à jour. Vérifie ta connexion internet.",
            "Updates konnten nicht geprüft werden. Prüfe deine Internetverbindung.");
        Add("Err_CannotOpen", "Não foi possível abrir {0}.", "Couldn't open {0}.", "No se pudo abrir {0}.", "Impossible d’ouvrir {0}.", "{0} konnte nicht geöffnet werden.");
        Add("Err_FolderNotFound", "A pasta deste jogo não foi encontrada.", "This game's folder wasn't found.",
            "No se encontró la carpeta de este juego.", "Le dossier de ce jeu est introuvable.", "Der Ordner dieses Spiels wurde nicht gefunden.");

        // ---------------- diagnóstico (leitura da Steam)
        Add("Log_SteamNotFound", "Steam não encontrada (nem no Registo nem na pasta por defeito).",
            "Steam not found (neither in the Registry nor in the default folder).",
            "No se encontró Steam (ni en el Registro ni en la carpeta predeterminada).",
            "Steam introuvable (ni dans le Registre ni dans le dossier par défaut).",
            "Steam nicht gefunden (weder in der Registry noch im Standardordner).");
        Add("Log_SteamFolder", "Pasta da Steam: {0}", "Steam folder: {0}", "Carpeta de Steam: {0}", "Dossier Steam : {0}", "Steam-Ordner: {0}");
        Add("Log_Libraries", "Bibliotecas encontradas: {0}", "Libraries found: {0}", "Bibliotecas encontradas: {0}", "Bibliothèques trouvées : {0}", "Gefundene Bibliotheken: {0}");
        Add("Log_NoSteamapps", "  {0} → sem pasta steamapps (disco desligado?)", "  {0} → no steamapps folder (drive disconnected?)",
            "  {0} → sin carpeta steamapps (¿disco desconectado?)", "  {0} → pas de dossier steamapps (disque débranché ?)", "  {0} → kein steamapps-Ordner (Laufwerk getrennt?)");
        Add("Log_ReadError", "  {0} → erro a ler: {1}", "  {0} → read error: {1}", "  {0} → error de lectura: {1}", "  {0} → erreur de lecture : {1}", "  {0} → Lesefehler: {1}");
        Add("Log_LibGames", "  {0} → {1} jogos", "  {0} → {1} games", "  {0} → {1} juegos", "  {0} → {1} jeux", "  {0} → {1} Spiele");
        Add("Log_NoLibraryVdf", "libraryfolders.vdf não existe — só a biblioteca principal será lida.",
            "libraryfolders.vdf doesn't exist — only the main library will be read.",
            "libraryfolders.vdf no existe: solo se leerá la biblioteca principal.",
            "libraryfolders.vdf n’existe pas — seule la bibliothèque principale sera lue.",
            "libraryfolders.vdf fehlt – nur die Hauptbibliothek wird gelesen.");
        Add("Log_LibraryVdfError", "Erro a ler libraryfolders.vdf: {0}", "Error reading libraryfolders.vdf: {0}", "Error al leer libraryfolders.vdf: {0}", "Erreur de lecture de libraryfolders.vdf : {0}", "Fehler beim Lesen von libraryfolders.vdf: {0}");
        Add("Log_NoLocalConfig", "Tempo de jogo da Steam: localconfig.vdf não encontrado.", "Steam playtime: localconfig.vdf not found.",
            "Tiempo de juego de Steam: no se encontró localconfig.vdf.", "Temps de jeu Steam : localconfig.vdf introuvable.", "Steam-Spielzeit: localconfig.vdf nicht gefunden.");
        Add("Log_PlaytimeRead", "Tempo de jogo da Steam lido da conta {0} ({1} entradas).", "Steam playtime read from account {0} ({1} entries).",
            "Tiempo de juego de Steam leído de la cuenta {0} ({1} entradas).", "Temps de jeu Steam lu depuis le compte {0} ({1} entrées).", "Steam-Spielzeit aus Konto {0} gelesen ({1} Einträge).");
        Add("Log_PlaytimeError", "Erro a ler o tempo de jogo da Steam: {0}", "Error reading Steam playtime: {0}", "Error al leer el tiempo de juego de Steam: {0}", "Erreur de lecture du temps de jeu Steam : {0}", "Fehler beim Lesen der Steam-Spielzeit: {0}");
        Add("Log_EpicNotFound", "Epic Games: launcher não encontrado.", "Epic Games: launcher not found.", "Epic Games: no se encontró el launcher.", "Epic Games : launcher introuvable.", "Epic Games: Launcher nicht gefunden.");
        Add("Log_UbisoftNotFound", "Ubisoft Connect: launcher não encontrado.", "Ubisoft Connect: launcher not found.", "Ubisoft Connect: no se encontró el launcher.", "Ubisoft Connect : launcher introuvable.", "Ubisoft Connect: Launcher nicht gefunden.");
        Add("Log_ScanTime", "Procura de jogos: {0} jogos em {1} ms", "Game scan: {0} games in {1} ms", "Búsqueda de juegos: {0} juegos en {1} ms",
            "Recherche de jeux : {0} jeux en {1} ms", "Spielsuche: {0} Spiele in {1} ms");
        Add("Log_StoreGames", "{0}: {1} jogos", "{0}: {1} games", "{0}: {1} juegos", "{0} : {1} jeux", "{0}: {1} Spiele");
        Add("Log_EpicPlaytimeRead", "Epic Games: tempo de jogo lido da conta ({0} jogos).", "Epic Games: playtime read from account ({0} games).", "Epic Games: tiempo de juego leído de la cuenta ({0} juegos).", "Epic Games : temps de jeu lu depuis le compte ({0} jeux).", "Epic Games: Spielzeit aus dem Konto gelesen ({0} Spiele).");
        Add("Log_EpicPlaytimeFailed", "Epic Games: não foi possível ler o tempo de jogo da conta.", "Epic Games: couldn't read playtime from the account.", "Epic Games: no se pudo leer el tiempo de juego de la cuenta.", "Epic Games : impossible de lire le temps de jeu du compte.", "Epic Games: Spielzeit konnte nicht aus dem Konto gelesen werden.");
        Add("Log_ManifestError", "Erro a ler {0}: {1}", "Error reading {0}: {1}", "Error al leer {0}: {1}", "Erreur de lecture de {0} : {1}", "Fehler beim Lesen von {0}: {1}");

        Instance = new Loc();
    }
}

/// <summary>Uso no XAML: Text="{l:Tr Nav_Home}". Atualiza-se sozinho quando a língua muda.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
