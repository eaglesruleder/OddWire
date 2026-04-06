# Plow System -- Programmer Specification

## 1. Purpose

The Plow system introduces a second soil layer ("Plowland") above
structural soil, separating:

-   Structural fertility (blockBelow)
-   Nutrient storage (Actual NPK)
-   Nutrient capacity (Fertility band)
-   Moisture retention (derived from blockBelow)

Plowing is a risk-reward action that: - Refills nutrient storage -
Potentially damages or improves structural soil - Creates a timing-based
agricultural loop - Couples fertiliser and compost systems to soil
progression

This is not a simple till mechanic.\
It is a structural exchange mechanic.

------------------------------------------------------------------------

## 2. Block Model

### Soil (blockBelow)

Stores: - FertilityBand (enum: VeryLow → TerraPeta) - No NPK storage -
No direct fertiliser storage

If FertilityBand \<= 0 → convert to BarrenSoil.

------------------------------------------------------------------------

### Plowland (blockAbove)

Stores: - ActualN, ActualP, ActualK - FertilityBand (controls NPK max
capacity) - Moisture (derived cap from blockBelow) - Crop compatibility
rules

Plowland does not drop itself when broken.

------------------------------------------------------------------------

## 3. Fertility & NPK Model

### Fertility Band

-   Hard capped at TerraPeta
-   Controls max NPK capacity
-   Example: TerraPeta → 80% NPK max

### Actual NPK

-   0 → 150%
-   Can exceed 100% only via fertiliser
-   Used for plow probability logic

------------------------------------------------------------------------

## 4. Plow Action Logic

### Preconditions

-   Target block must be Soil or Farmland.
-   SoilBelow.FertilityBand \> 0.

If plowing Farmland: - Convert underlying block to Soil. - Spawn
Plowland above.

------------------------------------------------------------------------

### Core Plow Algorithm

``` csharp
float avg = (ActualN + ActualP + ActualK) / 3f;
float roll = Rand.NextFloat() * 100f;
```

### Structural Drain Phase

If:

``` csharp
roll > avg
```

Then:

``` csharp
blockBelow.FertilityBand--;
```

Low actual NPK → higher structural damage chance.\
High actual NPK → structural protection.

------------------------------------------------------------------------

### Nutrient Restoration Phase

Always:

``` csharp
plowland.NPK.FillToMax();
```

Max determined by plowland.FertilityBand.

------------------------------------------------------------------------

### Critical Band Phase (100--150%)

If:

``` csharp
avg > 100
```

Then:

``` csharp
if (roll < avg - 100)
    blockBelow.FertilityBand += CritBonus;
```

Where: - CritBonus = 1 (default) - Optional CritBonus = 2 for
soil-engine progression

Clamp at TerraPeta.

------------------------------------------------------------------------

### Collapse Check

If:

``` csharp
blockBelow.FertilityBand <= 0
```

Convert to BarrenSoil.

------------------------------------------------------------------------

## 5. Moisture Model

Moisture cap derived from:

``` csharp
MoistureMax = f(blockBelow.FertilityBand);
```

Higher structural fertility → higher moisture retention.

------------------------------------------------------------------------

## 6. Crop Interaction Rules

All crops can grow in Plowland.

### Cereals

-   Higher yield on Plowland
-   Higher NPK consumption
-   Increased structural risk

### Roots

-   Moderate yield boost
-   Chance to improve FertilityBand
-   Rotation synergy

### Legumes

-   Restore NPK except own
-   Improve plow protection chance

### Fallow

-   Rapid NPK restoration
-   Improves SoilBelow over time
-   Rain boosts restoration

------------------------------------------------------------------------

## 7. Compost Interaction

Compost increases: - Actual NPK - Possibly FertilityBand (if
implemented)

Compost does not directly modify SoilBelow.

Structural improvement must occur through plow crit logic.

------------------------------------------------------------------------

## 8. Economic Intent

Plowing is not a pure upgrade and not deterministic.

It is:

A probabilistic structural exchange mechanic that refills nutrient
storage while conditionally degrading or improving deep soil fertility.

------------------------------------------------------------------------

## 9. System Boundaries

-   FertilityBand capped at TerraPeta.
-   NPK capped at 150%.
-   Crit chance capped at 50%.
-   Collapse threshold enforced.

System is bounded and non-duplicative.

------------------------------------------------------------------------

## 10. Summary

Plowland is a high-risk, high-ceiling farming layer above structural
soil.\
Proper rotation and fertiliser investment enable bounded long-term soil
progression.
