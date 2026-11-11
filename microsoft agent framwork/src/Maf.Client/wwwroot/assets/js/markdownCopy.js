window.mafMarkdown = {
    show: function (button, view) {
        var block = button.closest(".code-block");
        if (!block) {
            return;
        }

        var modes = block.querySelectorAll(".code-block-mode");
        for (var index = 0; index < modes.length; index++) {
            var active = modes[index] === button;
            modes[index].classList.toggle("is-active", active);
            modes[index].setAttribute("aria-pressed", active ? "true" : "false");
        }

        block.classList.toggle("is-code", view === "code");
    },
    copy: function (button) {
        var block = button.closest(".code-block");
        var code = block ? block.querySelector("pre code") : null;
        var text = code ? code.textContent : "";
        if (!navigator.clipboard) {
            return;
        }

        navigator.clipboard.writeText(text).then(function () {
            button.classList.add("is-copied");
            button.setAttribute("aria-label", "کپی شد");
            window.setTimeout(function () {
                if (!button.isConnected) {
                    return;
                }

                button.classList.remove("is-copied");
                button.setAttribute("aria-label", "کپی");
            }, 1500);
        });
    }
};
