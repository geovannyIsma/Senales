from sqlalchemy import create_engine, Column, Integer, String, Float, Boolean, DateTime, ForeignKey, Text
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker, relationship
from datetime import datetime

DATABASE_URL = "sqlite:///./metricas.db"

engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()

# ============== MODELOS ==============

class Estudiante(Base):
    __tablename__ = "estudiantes"
    
    id = Column(Integer, primary_key=True, index=True)
    nombre = Column(String(100), nullable=False)
    identificador = Column(String(50), unique=True, index=True)
    fecha_registro = Column(DateTime, default=datetime.utcnow)
    
    # Relación con sesiones
    sesiones = relationship("Sesion", back_populates="estudiante")


class Sesion(Base):
    __tablename__ = "sesiones"
    
    id = Column(Integer, primary_key=True, index=True)
    estudiante_id = Column(Integer, ForeignKey("estudiantes.id"), nullable=False)
    fecha_inicio = Column(DateTime, default=datetime.utcnow)
    fecha_fin = Column(DateTime, nullable=True)
    duracion_segundos = Column(Float, default=0)
    
    # Métricas de rendimiento
    total_aciertos = Column(Integer, default=0)
    total_errores = Column(Integer, default=0)
    tiempo_promedio_respuesta = Column(Float, default=0)
    
    # Progreso
    zonas_completadas = Column(Integer, default=0)
    zona_maxima_alcanzada = Column(Integer, default=0)
    
    # Dificultad
    dificultad_inicial = Column(Integer, default=0)  # 0=Baja, 1=Media, 2=Alta
    dificultad_final = Column(Integer, default=0)
    
    # Estado
    completada = Column(Boolean, default=False)
    datos_suficientes = Column(Boolean, default=False)  # True si hay suficientes intentos para análisis
    
    # Relaciones
    estudiante = relationship("Estudiante", back_populates="sesiones")
    intentos = relationship("IntentoSenal", back_populates="sesion")
    errores = relationship("ErrorDetallado", back_populates="sesion")
    ajustes = relationship("AjusteDificultad", back_populates="sesion")


class IntentoSenal(Base):
    __tablename__ = "intentos_senal"
    
    id = Column(Integer, primary_key=True, index=True)
    sesion_id = Column(Integer, ForeignKey("sesiones.id"), nullable=False)
    timestamp = Column(DateTime, default=datetime.utcnow)
    
    nombre_senal = Column(String(100), nullable=False)
    respuesta_usuario = Column(String(100), nullable=True)
    fue_correcta = Column(Boolean, nullable=False)
    tiempo_respuesta = Column(Float, nullable=False)
    
    zona = Column(Integer, default=0)
    ronda = Column(Integer, default=0)
    dificultad = Column(Integer, default=0)
    
    # Relación
    sesion = relationship("Sesion", back_populates="intentos")


class ErrorDetallado(Base):
    __tablename__ = "errores_detallados"
    
    id = Column(Integer, primary_key=True, index=True)
    sesion_id = Column(Integer, ForeignKey("sesiones.id"), nullable=False)
    timestamp = Column(DateTime, default=datetime.utcnow)
    
    nombre_senal = Column(String(100), nullable=False)
    respuesta_usuario = Column(String(100), nullable=True)
    tipo_error = Column(String(50), nullable=False)  # "confusion", "tiempo_agotado", "distractor"
    tiempo_respuesta = Column(Float, default=0)
    
    zona = Column(Integer, default=0)
    dificultad = Column(Integer, default=0)
    intentos_previos = Column(Integer, default=0)
    feedback_generado = Column(Text, nullable=True)
    
    # Relación
    sesion = relationship("Sesion", back_populates="errores")


class AjusteDificultad(Base):
    __tablename__ = "ajustes_dificultad"
    
    id = Column(Integer, primary_key=True, index=True)
    sesion_id = Column(Integer, ForeignKey("sesiones.id"), nullable=False)
    timestamp = Column(DateTime, default=datetime.utcnow)
    
    dificultad_anterior = Column(Integer, nullable=False)
    dificultad_nueva = Column(Integer, nullable=False)
    motivo = Column(String(200), nullable=False)
    
    tasa_aciertos = Column(Float, default=0)
    tiempo_promedio = Column(Float, default=0)
    zona = Column(Integer, default=0)
    ronda = Column(Integer, default=0)
    
    # Relación
    sesion = relationship("Sesion", back_populates="ajustes")


class ConfiguracionEvaluacion(Base):
    __tablename__ = "configuracion_evaluacion"
    
    id = Column(Integer, primary_key=True, index=True)
    nombre_configuracion = Column(String(100), default="default")
    activa = Column(Boolean, default=True)
    fecha_modificacion = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    # Cantidad de señales por dificultad
    senales_dificultad_baja = Column(Integer, default=3)
    senales_dificultad_media = Column(Integer, default=5)
    senales_dificultad_alta = Column(Integer, default=7)
    
    # Tiempo por dificultad (segundos)
    tiempo_dificultad_baja = Column(Float, default=12.0)
    tiempo_dificultad_media = Column(Float, default=8.0)
    tiempo_dificultad_alta = Column(Float, default=5.0)
    
    # Configuración general
    dificultad_inicial = Column(Integer, default=0)  # 0=Baja
    rondas_por_zona = Column(Integer, default=6)
    rondas_minimas_para_completar = Column(Integer, default=4)  # NUEVO
    tasa_aciertos_minima = Column(Float, default=0.7)
    
    # Configuración del modelo ML
    usar_modelo_ml = Column(Boolean, default=True)  # NUEVO: Siempre usar ML
    url_servidor_ml = Column(String(200), default="http://127.0.0.1:8000")  # NUEVO


# ============== FUNCIONES DE UTILIDAD ==============

def get_db():
    """Dependency para obtener sesión de base de datos"""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()


def init_db():
    """Inicializa la base de datos y crea las tablas"""
    Base.metadata.create_all(bind=engine)
    
    # Crear configuración por defecto si no existe
    db = SessionLocal()
    try:
        config = db.query(ConfiguracionEvaluacion).filter(
            ConfiguracionEvaluacion.activa == True
        ).first()
        
        if not config:
            config_default = ConfiguracionEvaluacion(
                nombre_configuracion="default",
                activa=True
            )
            db.add(config_default)
            db.commit()
            print("Configuración por defecto creada")
        
        # Crear estudiante por defecto si no existe
        estudiante = db.query(Estudiante).filter(Estudiante.id == 1).first()
        if not estudiante:
            estudiante_default = Estudiante(
                nombre="Estudiante VR",
                identificador="estudiante_vr_001"
            )
            db.add(estudiante_default)
            db.commit()
            print("Estudiante por defecto creado")
            
    finally:
        db.close()
