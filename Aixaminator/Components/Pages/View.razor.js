export function scrollToTop(selector) {
    const element = document.querySelector(selector);
    if (element) {
        element.scrollTop = 0;
    }
}

// Helper object for easier calling from .NET
export const ViewHelpers = {
    scrollToTop: scrollToTop
}; 