document.addEventListener("DOMContentLoaded", function () {
    const refreshTicketsBtn = document.getElementById("refreshTicketsBtn");
    const refreshTicketsForm = document.getElementById("refreshTicketsForm");
    refreshTicketsForm.addEventListener("submit", function (e) {
        e.preventDefault();

        refreshTicketsBtn.disabled = true;
        refreshTicketsBtn.innerText = "Refreshing tickets...";

        const xhr = new XMLHttpRequest();
        xhr.open("POST", refreshTicketsForm.action, true);

        xhr.onload = function () {
            if (xhr.status === 200) {
                sessionStorage.setItem("refreshedTickets", "true");
                window.location.href = window.location.href;
            }
        };

        xhr.send();
    });

    // Toast display on load
    if (sessionStorage.getItem("refreshedTickets") === "true") {
        const toastEl = document.getElementById("homeToast");
        const toastBody = toastEl.querySelector(".toast-body");
        toastBody.textContent = "Refreshed tickets!";
        const toast = new bootstrap.Toast(toastEl);
        toast.show();
        sessionStorage.removeItem("refreshedTickets");
    }
}); 
