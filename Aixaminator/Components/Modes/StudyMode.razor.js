export class StudyModeHelpers {
    static scrollToTop(selector) {
        let elem = document.querySelector(selector);
        if (elem) {
            elem.scrollTop = 0;
        }
    }
} 