// Day/night theme, persisted in localStorage. The .night class lives on
// <html> so CSS (theme.css) drives everything; no Blazor state needed to render.
window.narratumTheme = {
    toggle: function () {
        var night = document.documentElement.classList.toggle('night');
        try { localStorage.setItem('narratum-theme', night ? 'night' : 'day'); } catch (e) { }
        return night;
    },
    isNight: function () {
        return document.documentElement.classList.contains('night');
    }
};
