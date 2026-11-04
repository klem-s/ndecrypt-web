function fmtSize(bytes) {
  const units = ["o", "Ko", "Mo", "Go"];
  let v = bytes, i = 0;
  while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
  return v.toFixed(1) + " " + units[i];
}

let files = [];

async function load() {
  const res = await fetch("/api/files");
  files = await res.json();
  render();
}

function render() {
  const filter = document.getElementById("filter").value.toLowerCase();
  const rows = document.getElementById("rows");
  rows.innerHTML = "";
  for (const f of files) {
    if (filter && !f.path.toLowerCase().includes(filter)) continue;
    const tr = document.createElement("tr");

    const tdPath = document.createElement("td");
    tdPath.textContent = f.path;
    tr.appendChild(tdPath);

    const tdSize = document.createElement("td");
    tdSize.className = "size";
    tdSize.textContent = fmtSize(f.size);
    tr.appendChild(tdSize);

    const tdAction = document.createElement("td");
    const btn = document.createElement("button");
    btn.textContent = f.decrypted ? "Re-décrypter" : "Décrypter";
    const forceLbl = document.createElement("label");
    forceLbl.className = "force-lbl";
    const forceChk = document.createElement("input");
    forceChk.type = "checkbox";
    forceLbl.appendChild(forceChk);
    forceLbl.append(" forcer");
    tdAction.appendChild(btn);
    tdAction.appendChild(forceLbl);
    tr.appendChild(tdAction);

    const tdStatus = document.createElement("td");
    tdStatus.className = "status";
    if (f.decrypted) {
      const a = document.createElement("a");
      a.href = "/api/download?path=" + encodeURIComponent(f.path);
      a.textContent = "déjà décrypté — télécharger";
      a.style.color = "#51cf66";
      tdStatus.appendChild(a);
    }
    tr.appendChild(tdStatus);

    btn.addEventListener("click", async () => {
      btn.disabled = true;
      tdStatus.className = "status";
      tdStatus.textContent = "décryptage en cours...";
      try {
        const res = await fetch("/api/decrypt", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ path: f.path, force: forceChk.checked }),
        });
        const data = await res.json();
        if (res.ok) {
          tdStatus.className = "status ok";
          tdStatus.innerHTML = "";
          const a = document.createElement("a");
          a.href = "/api/download?path=" + encodeURIComponent(f.path);
          a.textContent = "OK — télécharger (" + fmtSize(data.size) + ")";
          a.style.color = "#51cf66";
          tdStatus.appendChild(a);
          f.decrypted = true;
        } else {
          tdStatus.className = "status err";
          tdStatus.textContent = (data.error || "erreur") + "\n" + (data.stderr || data.stdout || "");
        }
      } catch (e) {
        tdStatus.className = "status err";
        tdStatus.textContent = String(e);
      } finally {
        btn.disabled = false;
      }
    });

    rows.appendChild(tr);
  }
}

document.getElementById("filter").addEventListener("input", render);
load();
