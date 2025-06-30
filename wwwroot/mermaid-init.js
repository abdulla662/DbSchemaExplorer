window.renderMermaid = function () {
    setTimeout(() => {
        if (window.mermaid) {
            mermaid.initialize({ startOnLoad: false });
            mermaid.init(undefined, ".mermaid");
        }
    }, 1000); 
};
