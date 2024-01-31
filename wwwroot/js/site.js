// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function spinnerShow() {
    $("#loadingOverlay").addClass("show");
}

// Xóa lớp để ẩn spinner
function spinnerHide() {
    $("#loadingOverlay").removeClass("show");
}