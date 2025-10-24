document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("assignRoleForm");
    const techLink = document.getElementById("assignTechLink");
    const removeLink = document.getElementById("removeTechLink");

    async function postRoleChange(actionUrl, linkEl, busyText) {
        try {
            linkEl.classList.add("disabled");
            linkEl.textContent = busyText;

            const resp = await fetch(actionUrl, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "RequestVerificationToken": form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? ""
                },
                body: new FormData(form)
            });

            if (resp.ok) {
                const data = await resp.json();
                const role = (data && data.role) ? data.role : "User";
                sessionStorage.setItem("roleUpdated", role);
            } else {
                sessionStorage.setItem("roleUpdateMsg", "Failed to update role.");
            }
        } catch (err) {
            sessionStorage.setItem("roleUpdateMsg", err?.message || "Error updating role.");
        } finally {
            window.location.href = "/";
        }
    }

    if (techLink && form) {
        techLink.addEventListener("click", (e) => {
            e.preventDefault();
            postRoleChange(techLink.dataset.actionUrl, techLink, "Updating role…");
        });
    }
    if (removeLink && form) {
        removeLink.addEventListener("click", (e) => {
            e.preventDefault();
            postRoleChange(removeLink.dataset.actionUrl, removeLink, "Updating role…");
        });
    }


    const toastEl = document.getElementById("homeToast");
    if (toastEl) {
        const updated = sessionStorage.getItem("roleUpdated");
        const msg = sessionStorage.getItem("roleUpdateMsg");
        if (updated || msg) {
            const toastBody = toastEl.querySelector(".toast-body");
            toastBody.textContent = updated
                ? `Role updated to ${updated}.`
                : msg;
            new bootstrap.Toast(toastEl).show();
            sessionStorage.removeItem("roleUpdated");
            sessionStorage.removeItem("roleUpdateMsg");
        }
    }

});