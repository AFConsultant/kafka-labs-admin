package com.citibike.neighborhood;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;
import java.time.LocalDateTime;
import java.util.List;

@Repository
public interface NeighborhoodDeparturesCountRepository extends JpaRepository<NeighborhoodDeparturesCount, NeighborhoodDeparturesCountId> {

}
