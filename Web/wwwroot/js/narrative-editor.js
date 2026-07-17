// Narrative Editor JavaScript Interop

window.getEditorContent = function(editorElement) {
    if (!editorElement) return '';
    return editorElement.innerHTML || '';
};

window.setEditorContent = function(editorElement, content) {
    if (!editorElement) return;
    editorElement.innerHTML = content;
};

window.getSelection = function(editorElement) {
    if (!editorElement) return null;

    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) {
        return { text: '', start: 0, end: 0 };
    }

    const range = selection.getRangeAt(0);
    const selectedText = range.toString();

    return {
        text: selectedText,
        start: range.startOffset,
        end: range.endOffset
    };
};

window.execCommand = function(command, value = null) {
    document.execCommand(command, false, value);
};

window.insertText = function(editorElement, text) {
    if (!editorElement) return;

    editorElement.focus();

    const selection = window.getSelection();
    if (!selection.rangeCount) return;

    const range = selection.getRangeAt(0);
    range.deleteContents();

    const textNode = document.createTextNode(text);
    range.insertNode(textNode);

    // Move cursor to end of inserted text
    range.setStartAfter(textNode);
    range.setEndAfter(textNode);
    selection.removeAllRanges();
    selection.addRange(range);
};

window.replaceSelection = function(editorElement, newText) {
    if (!editorElement) return;

    editorElement.focus();

    const selection = window.getSelection();
    if (!selection.rangeCount) return;

    const range = selection.getRangeAt(0);
    range.deleteContents();

    const textNode = document.createTextNode(newText);
    range.insertNode(textNode);

    // Move cursor to end of new text
    range.setStartAfter(textNode);
    range.setEndAfter(textNode);
    selection.removeAllRanges();
    selection.addRange(range);

    // Trigger input event
    const event = new Event('input', { bubbles: true });
    editorElement.dispatchEvent(event);
};

// Auto-save functionality
window.initAutoSave = function(editorElement, dotNetHelper, interval = 2000) {
    if (!editorElement) return;

    let timeoutId = null;

    const handleInput = () => {
        if (timeoutId) {
            clearTimeout(timeoutId);
        }

        timeoutId = setTimeout(() => {
            dotNetHelper.invokeMethodAsync('AutoSave');
        }, interval);
    };

    editorElement.addEventListener('input', handleInput);

    return {
        dispose: () => {
            editorElement.removeEventListener('input', handleInput);
            if (timeoutId) {
                clearTimeout(timeoutId);
            }
        }
    };
};

// Keyboard shortcuts
window.initEditorShortcuts = function(editorElement, dotNetHelper) {
    if (!editorElement) return;

    const handleKeyDown = (e) => {
        // Ctrl/Cmd + S: Save
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('SaveContent');
            return;
        }

        // Ctrl/Cmd + Z: Undo
        if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('Undo');
            return;
        }

        // Ctrl/Cmd + Shift + Z or Ctrl/Cmd + Y: Redo
        if ((e.ctrlKey || e.metaKey) && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) {
            e.preventDefault();
            dotNetHelper.invokeMethodAsync('Redo');
            return;
        }
    };

    editorElement.addEventListener('keydown', handleKeyDown);

    return {
        dispose: () => {
            editorElement.removeEventListener('keydown', handleKeyDown);
        }
    };
};

// Count words in text
window.countWords = function(text) {
    if (!text) return 0;

    // Strip HTML tags
    const cleanText = text.replace(/<[^>]*>/g, ' ');

    // Count words
    const words = cleanText.trim().split(/\s+/).filter(word => word.length > 0);
    return words.length;
};

// Character count (excluding HTML)
window.countCharacters = function(text) {
    if (!text) return 0;

    // Strip HTML tags
    const cleanText = text.replace(/<[^>]*>/g, '');
    return cleanText.length;
};

// Syntax highlighting for narrative elements
window.highlightNarrativeElements = function(editorElement) {
    if (!editorElement) return;

    const content = editorElement.innerHTML;

    // Highlight dialogue (text between quotes or em-dashes)
    const highlightedContent = content
        .replace(/(—[^—\n]+)/g, '<span class="dialogue">$1</span>')
        .replace(/("[^"]+?")/g, '<span class="dialogue">$1</span>')
        .replace(/(\[[^\]]+\])/g, '<span class="action">$1</span>');

    if (highlightedContent !== content) {
        // Save cursor position
        const selection = window.getSelection();
        const range = selection.rangeCount > 0 ? selection.getRangeAt(0) : null;
        const offset = range ? range.startOffset : 0;

        editorElement.innerHTML = highlightedContent;

        // Restore cursor position
        if (range && editorElement.firstChild) {
            try {
                range.setStart(editorElement.firstChild, Math.min(offset, editorElement.firstChild.length));
                range.collapse(true);
                selection.removeAllRanges();
                selection.addRange(range);
            } catch (e) {
                // Ignore cursor restoration errors
            }
        }
    }
};

// Scroll to position
window.scrollToPosition = function(editorElement, position) {
    if (!editorElement) return;

    editorElement.scrollTo({
        top: position,
        behavior: 'smooth'
    });
};

// Get scroll position
window.getScrollPosition = function(editorElement) {
    if (!editorElement) return 0;
    return editorElement.scrollTop;
};
