window.renderMermaid = () => {
    if (window.mermaid) {
        mermaid.initialize({ startOnLoad: false });
        mermaid.init(undefined, document.querySelectorAll(".mermaid"));
    } else {
        console.error("❌ Mermaid.js not loaded.");
    }
};
