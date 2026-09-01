package reference;

/** The rule this reference repository exists to hold: a percentage off a price. */
public final class Discount {

    private Discount() {
    }

    public static long applyDiscount(long priceInCents, int percentOff) {
        if (percentOff < 0 || percentOff > 100) {
            throw new IllegalArgumentException("percentOff must be between 0 and 100");
        }
        return priceInCents * (100 - percentOff) / 100;
    }
}
