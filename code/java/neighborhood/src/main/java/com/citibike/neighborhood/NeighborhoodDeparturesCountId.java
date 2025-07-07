package com.citibike.neighborhood;

import java.io.Serializable;
import java.time.LocalDateTime;
import java.util.Objects;
import jakarta.persistence.*;

class NeighborhoodDeparturesCountId implements Serializable {
    @Column(name = "\"NEIGHBORHOOD\"", nullable = false)
    private String neighborhood;

    @Column(name = "\"WINDOW_START\"", nullable = false)
    private LocalDateTime windowStart;

    // Default constructor
    public NeighborhoodDeparturesCountId() {}

    // Parameterized constructor
    public NeighborhoodDeparturesCountId(String neighborhood, LocalDateTime windowStart) {
        this.neighborhood = neighborhood;
        this.windowStart = windowStart;
    }

    // Getters and setters
    public String getNeighborhood() {
        return neighborhood;
    }

    public void setNeighborhood(String neighborhood) {
        this.neighborhood = neighborhood;
    }

    public LocalDateTime getWindowStart() {
        return windowStart;
    }

    public void setWindowStart(LocalDateTime windowStart) {
        this.windowStart = windowStart;
    }

    // equals and hashCode are essential for composite keys
    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (o == null || getClass() != o.getClass()) return false;
        NeighborhoodDeparturesCountId that = (NeighborhoodDeparturesCountId) o;
        return Objects.equals(neighborhood, that.neighborhood) &&
               Objects.equals(windowStart, that.windowStart);
    }

    @Override
    public int hashCode() {
        return Objects.hash(neighborhood, windowStart);
    }
}