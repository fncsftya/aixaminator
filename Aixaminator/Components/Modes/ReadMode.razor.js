export class ReadModeHelpers {
    static dotNetHelper;

    static setDotNetHelper(value) {
        ReadModeHelpers.dotNetHelper = value;
    }

    static clearSelection() {
        const selection = window.getSelection();
        selection.removeAllRanges();
    }

    static lookUp(node) {
        if (node.tagName === "SPAN") {
            return node.parentNode;
        }
        return node;
    }

    static rangeToObj(range) {
        let startNode = range.startContainer;
        let endNode = range.endContainer;
        let startKey = parseInt(ReadModeHelpers.lookUp(range.startContainer.parentNode).dataset.key);
        let endKey = parseInt(ReadModeHelpers.lookUp(range.endContainer.parentNode).dataset.key);
        let startOffset = range.startOffset;
        let endOffset = range.endOffset;

        return {
            startKey,
            endKey,
            startOffset: startOffset + ReadModeHelpers.getPreviousSiblingsTextLength(startNode),
            endOffset: endOffset + ReadModeHelpers.getPreviousSiblingsTextLength(endNode)
        };
    }

    static async highlighter(e) {
        let hasSelection = false;
        const selection = window.getSelection();
        const range = selection.rangeCount > 0 ? selection.getRangeAt(0) : null;
        if (range) {
            const container = document.querySelector(".reader > article");
            if (container != null && container.contains(range.commonAncestorContainer)) {
                const text = selection.toString();
                if (text != null && text.length > 0) {
                    const location = ReadModeHelpers.rangeToObj(range);
                    await ReadModeHelpers.dotNetHelper.invokeMethodAsync("EnableHighlightMenu", text, JSON.stringify(location));
                    hasSelection = true;
                }
            }
        }

        if (!hasSelection) {
            await ReadModeHelpers.dotNetHelper.invokeMethodAsync("DisableHighlightMenu");
        } else {
            // mouseup happens before window realises the selection has gone,
            // so wait a little before checking
            setTimeout(async () => {
                if (window.getSelection().rangeCount === 0) {
                    await ReadModeHelpers.dotNetHelper.invokeMethodAsync("DisableHighlightMenu");
                }
            }, 250);
        }
    }

    static getPreviousSiblingsTextLength(node) {
        let length = 0;

        if (node.parentNode.tagName === "SPAN" && node.previousSibling == null) {
            node = node.parentNode;
        }

        while (node.previousSibling != null) {
            length += node.previousSibling.textContent.length;
            node = node.previousSibling;
        }

        return length;
    }

    static scrollToTop(selector) {
        let elem = document.querySelector(selector);
        if (elem) {
            elem.scrollTop = 0;
        }
    }
}

export function addHandlers() {
    document.addEventListener("mouseup", ReadModeHelpers.highlighter);
}

export function removeHandlers() {
    document.removeEventListener("mouseup", ReadModeHelpers.highlighter);
} 