const API_BASE = "http://localhost:5100";

async function callApi(method, path) {
    const response = await fetch(API_BASE + path, { method });
    const data = await response.json().catch(() => null);
    if (!response.ok) throw new Error(data?.message ?? response.statusText);
    return data;
}

function render(status) {
    document.getElementById("phase").textContent = status.phase;
    document.getElementById("tick").textContent = status.tick;
    document.getElementById("ore").textContent = status.ore;
    document.getElementById("units").textContent = status.units;
    document.getElementById("log").textContent = status.log.join("\n");

    const watch = document.getElementById("watch-link");
    watch.hidden = !status.watchUrl;
    if (status.watchUrl) watch.href = status.watchUrl;

    document.getElementById("start-button").disabled = status.isRunning;
    document.getElementById("stop-button").disabled = !status.isRunning;
}

async function refreshStatus() {
    const indicator = document.getElementById("api-indicator");
    try {
        render(await callApi("GET", "/api/bot/status"));
        indicator.textContent = "online";
    } catch {
        indicator.textContent = "offline";
    }
}

document.getElementById("start-button").addEventListener("click", async () => {
    try { render(await callApi("POST", "/api/bot/start")); }
    catch (error) { alert("Could not start: " + error.message); }
});

document.getElementById("stop-button").addEventListener("click", async () => {
    render(await callApi("POST", "/api/bot/stop"));
});

refreshStatus();
setInterval(refreshStatus, 1000);