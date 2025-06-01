document.addEventListener("DOMContentLoaded", function () {
    const fileInput = document.getElementById("fileInput");
    const uploadStatus = document.getElementById("uploadStatus");
    const fileLimit = document.getElementById("fileLimit");
    const resetButton = document.getElementById("resetBtn");
    const progressBar = document.getElementById("uploadProgress");
    const uploadForm = document.getElementById("ticketForm");
    const submitBtn = document.getElementById("submitBtn");
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png'];

    if (uploadForm) {
        if (fileInput) {
            fileInput.addEventListener('change', function () {
                const selectedFiles = Array.from(fileInput.files);

                //Validation
                const invalidFile = selectedFiles.find(file => !allowedTypes.includes(file.type.toLowerCase()));
                if (invalidFile) {
                    alert('Only JPG, JPEG and PNG files are allowed');
                    fileInput.value = '';
                    return;
                }

                //Limit to 2 screenshots
                if (selectedFiles.length > 2) {
                    fileInput.value = '';
                    alert('A maximum of 2 screenshots are allowed.');
                    return
                } else if(selectedFiles.length == 2) {
                    fileInput.disabled = true;
                    fileLimit.innerText = 'Limit of 2 screenshots reached.';
                    return;
                }
            });

            resetButton.addEventListener('click', function () {
                fileInput.disabled = false;
                fileInput.value = '';
                uploadStatus.innerText = '';
                fileLimit.innerText = '';
                progressBar.value = 0;
            });
        }
        uploadForm.addEventListener("submit", function (e) {
            e.preventDefault();
            submitBtn.disabled = true;
            submitBtn.innerText = "Uploading..."; 

            const formData = new FormData(uploadForm);

            // Optional: validate again if needed
            if (Array.from(fileInput.files).length > 1) {
                Array.from(fileInput.files).forEach(file => {
                    formData.append("upload", file);
                });
            }

            const xhr = new XMLHttpRequest();
            xhr.open("POST", uploadForm.action, true);

            xhr.upload.addEventListener("progress", function (e) {
                if (e.lengthComputable) {
                    const percent = Math.round((e.loaded / e.total) * 100);
                    progressBar.value = percent;
                }
            });

            xhr.onload = function () {
                if (xhr.status === 200) {
                    sessionStorage.setItem("ticketSubmitted", "true");
                    window.location.href = "/User/UserDashboard";     
                } else {
                    uploadStatus.innerText = "Error submitting ticket.";
                }
            };

            xhr.onerror = function () {
                uploadStatus.innerText = "An error occurred during upload.";
            };

            xhr.send(formData);
        });
    }

    if (sessionStorage.getItem("ticketSubmitted") === "true") {
        const toastEl = document.getElementById("homeToast");
        const toastBody = toastEl.querySelector(".toast-body");
        toastBody.textContent = "Ticket submitted successfully!";
        const toast = new bootstrap.Toast(toastEl);
        toast.show();
        sessionStorage.removeItem("ticketSubmitted");
    }
});