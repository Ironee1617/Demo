namespace PrototypeLab.Aquarium
{
    // R26: coral colony heads (x, top of colony, z), baked from AquariumEnvironment.Create by clustering the
    // coral meshes (reef rocks, planting pockets, shoreline beds, monumental reefs). Used as clownfish visiting
    // spots (AquariumAnemoneLayout.Corals). Re-bake if the reefs are re-authored. 155 heads.
    internal static class CoralTable
    {
        internal static readonly Point[] Points =
        {
            new Point(-33.05f, 0.86f, 4.30f), new Point(-32.61f, 0.76f, 6.80f), new Point(-32.30f, 0.71f, 8.81f), new Point(-32.19f, 0.75f, 1.26f),
            new Point(-31.19f, 0.82f, 11.08f), new Point(-31.06f, 0.97f, -2.22f), new Point(-29.58f, 0.79f, 13.19f), new Point(-29.11f, 0.92f, -4.95f),
            new Point(-27.81f, 0.76f, 15.22f), new Point(-27.39f, 3.01f, -1.21f), new Point(-27.28f, 2.86f, -5.67f), new Point(-27.26f, 3.79f, -4.06f),
            new Point(-26.75f, 0.93f, -7.33f), new Point(-26.84f, 6.97f, 1.69f), new Point(-26.67f, 6.42f, 2.51f), new Point(-26.48f, 1.99f, 7.11f),
            new Point(-25.81f, 2.92f, 10.03f), new Point(-25.72f, 1.89f, 6.21f), new Point(-25.59f, 0.83f, 17.44f), new Point(-25.54f, 6.72f, 3.28f),
            new Point(-25.32f, 4.57f, -4.20f), new Point(-25.29f, 6.82f, -2.28f), new Point(-25.08f, 4.34f, -2.97f), new Point(-25.07f, 3.66f, 9.22f),
            new Point(-24.87f, 8.71f, 1.26f), new Point(-24.61f, 2.26f, 3.80f), new Point(-24.38f, 2.71f, 6.52f), new Point(-24.11f, 3.34f, -5.90f),
            new Point(-24.04f, 0.77f, -9.17f), new Point(-23.48f, 4.91f, -3.35f), new Point(-23.37f, 1.59f, 7.43f), new Point(-23.32f, 6.67f, -2.27f),
            new Point(-23.19f, 7.69f, -1.16f), new Point(-22.96f, 0.85f, 15.72f), new Point(-22.75f, 8.30f, 1.70f), new Point(-22.77f, 0.66f, 19.39f),
            new Point(-22.42f, 3.56f, 11.18f), new Point(-22.01f, 3.18f, 12.01f), new Point(-21.91f, 2.69f, -3.60f), new Point(-21.50f, 2.11f, 7.78f),
            new Point(-21.45f, 7.42f, -1.16f), new Point(-21.41f, 6.94f, 0.24f), new Point(-21.37f, 3.32f, 10.55f), new Point(-21.30f, 1.98f, -7.92f),
            new Point(-21.11f, 2.56f, -5.36f), new Point(-20.92f, 2.54f, -4.09f), new Point(-20.49f, 3.09f, 14.49f), new Point(-20.27f, 2.74f, -5.30f),
            new Point(-20.16f, 0.83f, 21.02f), new Point(-18.56f, 1.60f, -6.48f), new Point(-18.27f, 0.82f, -11.70f), new Point(-17.96f, 0.89f, 21.78f),
            new Point(-17.91f, 1.78f, -9.81f), new Point(-17.21f, 1.28f, -2.21f), new Point(-16.68f, 0.94f, 21.61f), new Point(-16.45f, 1.17f, -2.50f),
            new Point(-16.01f, 1.04f, 21.65f), new Point(-15.81f, 0.86f, -12.43f), new Point(-15.02f, 1.31f, 11.92f), new Point(-14.70f, 3.22f, -5.01f),
            new Point(-14.37f, 2.76f, 10.95f), new Point(-13.87f, 0.73f, -14.16f), new Point(-12.91f, 0.70f, 21.64f), new Point(-12.64f, 0.80f, -15.59f),
            new Point(-12.05f, 1.94f, 8.38f), new Point(-11.35f, 0.69f, -16.57f), new Point(-11.35f, 3.36f, 8.89f), new Point(-10.49f, 2.08f, 9.58f),
            new Point(-10.34f, 1.32f, 10.72f), new Point(-10.19f, 1.00f, 20.54f), new Point(-9.72f, 1.05f, -16.99f), new Point(-7.72f, 0.79f, -16.14f),
            new Point(-7.36f, 0.71f, 18.54f), new Point(-5.95f, 1.04f, -14.92f), new Point(-5.52f, 3.21f, 14.47f), new Point(-4.40f, 0.85f, -13.83f),
            new Point(-3.96f, 1.54f, 0.87f), new Point(-3.70f, 2.23f, 2.41f), new Point(-2.29f, 1.20f, 1.23f), new Point(-2.06f, 5.94f, 4.55f),
            new Point(-1.56f, 5.40f, 3.11f), new Point(-0.52f, 6.01f, 3.60f), new Point(1.70f, 0.77f, -16.95f), new Point(1.79f, 6.16f, 2.26f),
            new Point(2.53f, 3.43f, -10.26f), new Point(3.62f, 3.21f, 9.58f), new Point(4.21f, 6.29f, 6.15f), new Point(4.21f, 0.80f, 13.06f),
            new Point(4.44f, 1.60f, -8.31f), new Point(4.58f, 1.01f, -18.33f), new Point(6.12f, 7.22f, 5.73f), new Point(6.28f, 0.83f, 11.57f),
            new Point(7.58f, 5.59f, 3.72f), new Point(8.02f, 0.96f, -19.24f), new Point(8.03f, 1.22f, 6.79f), new Point(8.08f, 3.76f, 2.22f),
            new Point(8.15f, 1.24f, 1.63f), new Point(9.53f, 1.46f, 4.36f), new Point(9.47f, 0.81f, 11.11f), new Point(9.62f, 0.97f, 7.87f),
            new Point(11.66f, 0.91f, -18.97f), new Point(11.75f, 1.01f, 2.10f), new Point(12.09f, 3.23f, 5.64f), new Point(12.20f, 0.97f, 10.25f),
            new Point(14.63f, 0.89f, 9.85f), new Point(15.56f, 0.96f, 8.74f), new Point(16.03f, 0.76f, -18.43f), new Point(16.72f, 0.82f, 8.31f),
            new Point(16.78f, 3.31f, -13.89f), new Point(17.78f, 0.90f, 8.74f), new Point(18.37f, 0.90f, 10.36f), new Point(19.30f, 1.02f, 11.55f),
            new Point(19.62f, 1.02f, -13.06f), new Point(19.71f, 0.91f, -16.73f), new Point(20.73f, 7.57f, 4.52f), new Point(21.21f, 1.43f, -8.83f),
            new Point(21.49f, 0.87f, -3.65f), new Point(21.49f, 6.06f, 1.89f), new Point(21.88f, 7.22f, 1.19f), new Point(21.92f, 8.06f, 4.29f),
            new Point(21.98f, 7.56f, 2.48f), new Point(22.01f, 0.88f, 13.08f), new Point(22.28f, 1.56f, -0.11f), new Point(23.05f, 7.40f, 0.92f),
            new Point(23.32f, 1.26f, -10.19f), new Point(23.48f, 1.10f, -6.36f), new Point(23.61f, 0.84f, -14.67f), new Point(23.92f, 8.61f, 2.69f),
            new Point(24.15f, 9.02f, 5.50f), new Point(24.39f, 7.85f, 0.33f), new Point(24.52f, 8.86f, 3.92f), new Point(24.56f, 1.05f, 12.59f),
            new Point(24.72f, 1.00f, 11.44f), new Point(24.93f, 2.17f, -0.55f), new Point(25.14f, 3.43f, 8.85f), new Point(25.24f, 1.75f, -9.95f),
            new Point(25.56f, 2.32f, -6.89f), new Point(26.13f, 9.37f, 6.04f), new Point(26.16f, 9.63f, 3.34f), new Point(26.26f, 2.58f, -8.34f),
            new Point(27.05f, 0.75f, -12.19f), new Point(27.37f, 9.43f, 1.82f), new Point(27.75f, 0.80f, 12.25f), new Point(28.19f, 9.96f, 3.23f),
            new Point(28.76f, 9.31f, 6.26f), new Point(29.05f, 5.04f, -0.97f), new Point(29.58f, 0.78f, -9.64f), new Point(29.56f, 5.06f, 0.34f),
            new Point(30.17f, 0.80f, 10.13f), new Point(32.10f, 0.69f, -6.86f), new Point(32.11f, 0.78f, 7.48f), new Point(33.51f, 0.61f, -4.02f),
            new Point(33.66f, 0.89f, 4.69f), new Point(34.29f, 0.87f, -1.16f), new Point(34.83f, 0.68f, 1.85f),
        };
    }
}
