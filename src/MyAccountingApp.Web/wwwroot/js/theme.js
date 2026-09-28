// Theme persistence on top of localStorage. localStorage can throw (private mode,
// blocked cookies), so every access is guarded: the app must still work without storage.
window.themeStore = {
    storageKey: "myaccountingapp.theme",

    get: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },

    set: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch (e) {
            // Nothing to do: the theme still applies for this session.
        }
    },

    systemPrefersDark: function () {
        return !!(window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches);
    },

    // Mirrors the mode onto <html data-theme> so the few styles MudBlazor does not own
    // (loading spinner, error banner) follow it as well.
    apply: function (isDark) {
        var root = document.documentElement;
        root.setAttribute("data-theme", isDark ? "dark" : "light");
        root.style.colorScheme = isDark ? "dark" : "light";
        root.style.backgroundColor = isDark ? "#1b1b1f" : "#ffffff";
    },

    // Called from index.html before Blazor boots, so the page never flashes white
    // on a dark system. Same resolution rules as ThemeResolver: an explicit "light" wins,
    // an explicit "dark" wins, anything else follows the system.
    init: function () {
        var stored = this.get(this.storageKey);
        var isDark = stored === "dark" || (stored !== "light" && this.systemPrefersDark());
        this.apply(isDark);
    }
};
