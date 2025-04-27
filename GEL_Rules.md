# Gene Expression Language (GEL) Specification

## Core Elements

### Markers
- **Basic Markers**: M0-M8 (primary genetic markers)
- **Special Markers**: 
  - Ms (stem cell marker)
  - Msk (skin marker)
  - Mfl (flesh cell marker)
  - tm0 (morphogen released when timer 0 is reached)
  - tm1 (morphogen released when timer 1 is reached)

### Inhibition
- **-M notation**: Pink-highlighted markers indicate inhibition, encoded with a "-" prefix
  - Example: -M2 means M2 is inhibited

### Parameters
- **n(x)**: Environmental conditions where:
  - n(0): Number of neighbors equals 0
  - n(6): Number of neighbors equals 6
- **Superscript Numbers**: Indicate intensity of activation or range when applicable

## Syntax

### Expression Format
- Basic expression: `[marker1 marker2 ... -markerX ... markerN]{R}`
- Positive markers indicate activation, negative markers indicate inhibition
- Both presence and absence of specific markers determine cell fate
- R is the range of the expression or in case of a timer, the step the timer is set to

## Phenotype Mapping

### Morphology (0-13)
- Different combinations of activated and inhibited markers produce specific morphologies
- Example: `[Ms M1 -M2 -M6]` → morph 0 (M1 active, M2 and M6 inhibited)

### Functional States (14-20)
- **Timers**: States 14-15, triggered by specific conditions
  - Timer 0 (state 14): Releases tm0 morphogen when reached
  - Timer 1 (state 15): Releases tm1 morphogen when reached
- **Division**: State 16, requires markers [Ms M0] [Msk] [Mf1 -M6]
- **Apoptosis**: State 17

- **Skin**: State 18, requires [Ms M1 -M6]
- **Flesh**: State 19, requires [Ms -M0 M6 M7 n(6)]
- **Movement**: State 20

## Expression Rules
1. Cell fate is determined by both activations and inhibitions
2. Stem cell marker (Ms) enables specialized functions and morphologies
3. Inhibition of specific markers (-M notation) is as important as activation
4. Environmental conditions and neighbor counts influence expression patterns


## Expression Examples

### Basic Expression
"M0" => [Ms M1 -M2 -M6] // Release M0 if the cases are met
"M1" => [Ms M1 M2 -M6] // Release M1 if the cases are met
"M2" => [Ms M1 -M2 -M6] // Release M2 if the cases are met
"M6" => [Ms M1 -M2 M6] // Release M6 if the cases are met
"APOP" => [Ms -M0 M6 M7 n(6)] // Commit Apoptosis if the cases are met
"NODIV" => [Ms M0] [Msk] [Mf1 -M6] // Inhibit Division if the cases are met
"SKIN" => [Ms M1 -M6] // Specilize to Skin if the cases are met
"FLESH" => [Ms -M0 M6 M7 n(6)] // Commit Flesh if the cases are met
"MOVE" => [Ms M0 M1 M2 M6 M7 n(6)]-M8 // Move away from the M8 gradient if the cases are met
"MOVE" => [Mfl -M0 M6 M7 n(6)]M3 // Move towards the M3 gradient if the cases are met