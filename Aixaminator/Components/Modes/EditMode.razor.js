export class EditModeHelpers {
    static scrollToTop(selector) {
        let elem = document.querySelector(selector);
        if (elem) {
            elem.scrollTop = 0;
        }
    }
} 