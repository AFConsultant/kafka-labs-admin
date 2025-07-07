package com.citibike.neighborhood;

import java.time.LocalDateTime;
import jakarta.persistence.*;

@Entity
@Table(name = "neighborhood_departures_count")
@IdClass(NeighborhoodDeparturesCountId.class)
public class NeighborhoodDeparturesCount {

    // Part of the composite primary key
    @Id
    @Column(name = "\"NEIGHBORHOOD\"")
    private String neighborhood;

    // Part of the composite primary key
    @Id
    @Column(name = "\"WINDOW_START\"")
    private LocalDateTime windowStart;

    @Column(name = "\"WINDOW_END\"")
    private LocalDateTime windowEnd;

    @Column(name = "\"DEPARTURES_COUNT\"")
    private Long departuresCount;

    public NeighborhoodDeparturesCount() {}

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

    public LocalDateTime getWindowEnd() {
        return windowEnd;
    }

    public void setWindowEnd(LocalDateTime windowEnd) {
        this.windowEnd = windowEnd;
    }

    public Long getDeparturesCount() {
        return departuresCount;
    }

    public void setDeparturesCount(Long departuresCount) {
        this.departuresCount = departuresCount;
    }
}