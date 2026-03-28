# Adaptive VR Rehabilitation System
## Revised Task Distribution and System Architecture Proposal

---

## 1. Introduction

This document proposes an updated division of work and system architecture for the development of the **Adaptive VR Rehabilitation System for Upper-Limb Recovery Post-Stroke**.

The revision improves:
- workload balance between team members
- alignment with individual academic tracks
- technical depth of the project
- inclusion of a data-driven adaptive component

---

## 2. System Overview

The system is designed as a **closed-loop adaptive rehabilitation platform**, where user performance continuously influences task difficulty.

### Core Workflow

User performs task -> movement is tracked -> trajectory is processed -> performance features are extracted -> data is stored -> adaptive model evaluates performance -> system adjusts difficulty

---

## 3. Key Design Principles

- Separation between **raw data acquisition** and **data interpretation**
- Use of **standardized performance metrics across tasks**
- Adoption of a **data-driven adaptive system**, extending the original rule-based approach
- Modular architecture: Unity (interaction & signal processing) + Backend (data & intelligence)

---

## 4. Revised Division of Work

### 4.1 Shared Responsibilities

Both students collaborate on:

- Design and implementation of VR environments:
  - Kitchen scene
  - Living room scene
  - Garden scene

- Development of rehabilitation tasks:
  - Watering plants
  - Picking objects
  - Hanging tools
  - Cleaning / manipulation tasks

- Definition of difficulty parameters:
  - target size
  - reach distance
  - task complexity
  - visual guidance

- System testing and usability validation

---

### 4.2 Mechatronics / Software (Student A)

Responsible for **interaction systems and raw data acquisition**

#### Motion Tracking
- Capture hand/controller position and movement (3D coordinates)
- Store trajectory temporarily during task execution
- Manage tracking lifecycle:
  - start tracking
  - update tracking
  - stop tracking

#### Interaction System
- Configure and standardize **SDK-based** reusable interaction components across tasks:
  - grab / release
  - place (snapping, validation)
  - pour (thresholds, spill logic)
  - cut
  - task-specific rules, edge cases, and telemetry around SDK events

#### Event Detection
- Detect and log:
  - task start / completion
  - errors
  - prompts / guidance usage

#### Unity Integration
- Integrate tracking and interaction systems within VR scenes
- Ensure stable and responsive real-time behavior

---

### 4.3 AI & Data Analysis (Student B)

Responsible for **performance modeling, data pipeline, and adaptive intelligence**

#### Performance Modeling (Core Contribution)
Define quantitative metrics derived from movement data:

- normalized completion time (task + difficulty based)
- error count
- prompt usage
- path efficiency
- movement smoothness
- hesitation / pauses
- accuracy

#### Feature Extraction Logic
- Design mathematical formulas for all metrics
- Collaborate on implementation within Unity
- Ensure consistency across tasks and sessions

#### Data Architecture
- Design relational database schema (PostgreSQL / Supabase)
- Structure entities:
  - patients (anonymized)
  - sessions
  - tasks
  - performance metrics
  - adaptation decisions

#### Backend System
- Develop API for:
  - receiving performance data from Unity
  - storing structured data
  - returning adaptive decisions

#### Adaptive System

**Phase 1: Rule-Based Baseline**
- Define initial decision rules:
  - high performance -> increase difficulty
  - moderate -> maintain
  - low -> decrease

**Phase 2: Data-Driven Model**
- Train machine learning model on collected data
- Input: performance features
- Output: difficulty adjustment decision

#### Data Analysis & Evaluation
- Analyze user performance trends
- Compare:
  - rule-based vs machine learning approach
- Evaluate system effectiveness and adaptability

---

## 5. Movement Tracking and Feature Extraction Methodology

The system performs **trajectory-based motion analysis** to derive meaningful performance metrics from raw movement data.

### 5.1 Trajectory Acquisition

- Raw 3D positions are recorded during task execution
- Data is stored temporarily in memory within Unity
- No per-frame data is directly stored in the database

---

### 5.2 Trajectory Preprocessing

To improve signal quality:

- **Downsampling** is applied to reduce redundant points
- **Smoothing techniques** (e.g., moving average or low-pass filtering) are used to reduce noise and jitter

---

### 5.3 Ideal Path Definition

For each task, an expected trajectory is defined:

- simple tasks -> straight-line path from start to target
- multi-step tasks -> sequence of target checkpoints

---

### 5.4 Feature Computation

The following metrics are computed from the processed trajectory:

#### Path Efficiency
Ratio between optimal path and actual path:

efficiency = straight-line distance / actual path length

---

#### Deviation from Ideal Path
- Average distance between actual trajectory and ideal path
- Measures spatial accuracy of movement

---

#### Movement Smoothness
Derived from kinematic properties:

- velocity variation
- acceleration changes
- optional jerk approximation

Smooth movement corresponds to low variability in motion

---

#### Speed Metrics
- average speed
- peak speed

---

#### Hesitation Detection
- number and duration of pauses
- based on velocity thresholds

---

### 5.5 Data Storage Strategy

Only **aggregated features** are stored in the database:

- completion time
- efficiency
- smoothness
- errors
- prompts
- accuracy
- hesitation (pause count, total pause duration)

Optional:
- raw trajectory may be stored separately (e.g., JSON or file storage) for offline analysis

---

### 5.6 Rationale

This approach:

- avoids storing large volumes of raw coordinate data
- ensures efficient database usage
- provides ML-ready structured features
- aligns with signal processing and motion analysis principles

---

## 6. System Architecture

### Unity (VR Layer)
- task execution
- motion tracking
- trajectory preprocessing
- feature computation

down

### Backend (API Layer)
- receives feature data
- stores data in database
- runs adaptive model inference

down

### Database (PostgreSQL / Supabase)
- stores structured performance data

down

### Machine Learning Pipeline (Offline)
- model training using collected dataset
- deployment for runtime inference

---

## 7. Adaptive Decision Process

At runtime:

1. Unity computes performance features
2. Features are sent to backend
3. Backend evaluates:
   - rule-based logic or trained model
4. Backend returns decision:
   - increase difficulty
   - maintain
   - decrease
5. Unity updates task parameters accordingly

---

## 8. Improvements Over Original Proposal

Compared to the initial project definition:

- VR development is now **shared**, improving workload balance
- The tracking system is extended into a **full motion analysis pipeline**
- The adaptive system evolves from **rule-based to data-driven**
- A complete **machine learning component** is introduced
- The system becomes a **closed-loop adaptive system**

---

## 9. Work Requirements

- Organized file structure in GitHub and Git LFS
- Agreed versioning across plugins and applications
- Efficient organization and consistency across previous and newly developed systems

## 10. Conclusion

This revised structure ensures:

- balanced distribution of workload
- clear separation of responsibilities
- strong alignment with academic tracks
- enhanced technical and research value

The project evolves from a VR application with tracking into a **data-driven adaptive rehabilitation system**, integrating motion analysis, performance modeling, and intelligent difficulty adaptation.



