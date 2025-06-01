document.addEventListener("DOMContentLoaded", function () {
    const closeButtons = document.querySelectorAll(".close-ticket-btn");

    closeButtons.forEach(button => {
        button.addEventListener("click", function () {
            const ticketId = this.dataset.ticketId;
            button.disabled = true;
            button.innerText = "Closing..."; 

            fetch("/Tickets/CloseTicket", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded"
                },
                body: `ticketId=${encodeURIComponent(ticketId)}`
            })
                .then(response => {
                    if (response.ok) {
                        sessionStorage.setItem("ticketClosed", "true");
                        window.location.href = window.location.href;
                    }
                });
        });
    });

    // Toast display on load
    if (sessionStorage.getItem("ticketClosed") === "true") {
        const toastEl = document.getElementById("homeToast");
        const toastBody = toastEl.querySelector(".toast-body");
        toastBody.textContent = "Ticket closed successfully!";
        const toast = new bootstrap.Toast(toastEl);
        toast.show();
        sessionStorage.removeItem("ticketClosed");
    }
});