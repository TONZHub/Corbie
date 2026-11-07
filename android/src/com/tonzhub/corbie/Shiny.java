package com.tonzhub.corbie;

enum Shiny {
    BOTTLE_CAP("Bottle cap", "Priceless. You may admire it from there."),
    PAPERCLIP("Bent paperclip", "The bend improves its structural sulk."),
    SEA_GLASS("Tumbled sea glass", "A bit of the sea. Mine now."),
    ESC_KEY("Broken Esc key", "There is no escape from your deadlines."),
    BRASS_NUT("Brass hex nut", "Precision engineering. Excellent for hoarding."),
    PURPLE_D20("Purple D20", "Natural twenty on scavenging.");

    private static final Shiny[] COLLECTION = values();
    final String label, remark;

    Shiny(String label, String remark) {
        this.label = label;
        this.remark = remark;
    }

    static Shiny next(int total) {
        return COLLECTION[total % COLLECTION.length];
    }

    int copies(int total) {
        return total / COLLECTION.length + (ordinal() < total % COLLECTION.length ? 1 : 0);
    }

    static Shiny restore(String saved) {
        for (Shiny item : COLLECTION) if (item.name().equals(saved)) return item;
        return null;
    }
}
